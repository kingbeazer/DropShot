using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public enum ScheduleDeleteMode
{
    /// <summary>Don't delete anything. Dedupe against existing fixtures and only
    /// create what's missing.</summary>
    None,
    /// <summary>Delete fixtures with no ScheduledAt before scheduling.
    /// Used by the setup page's "Delete existing" toggle.</summary>
    UnscheduledOnly,
    /// <summary>Delete any fixture that isn't Completed, InProgress, or
    /// AwaitingVerification. Used by the API endpoint when re-scheduling.</summary>
    AllIncomplete,
}

public record ScheduleFixturesRequest(ScheduleDeleteMode DeleteMode = ScheduleDeleteMode.None);

public record ScheduleFixturesResult(int FixturesCreated, int Unscheduled);

/// <summary>
/// Auto-scheduler for round-robin, knockout, semi-final, quarter-final and
/// final stages. Handles per-division competitions: each division gets its
/// own RR + knockout ladder, and fixtures are placed into time slots honouring
/// that division's match windows (plus any shared windows).
/// </summary>
/// <remarks>
/// Authorization is the caller's responsibility — this service only performs
/// the mechanical scheduling work against the database.
/// </remarks>
public class CompetitionSchedulerService(IDbContextFactory<MyDbContext> dbFactory)
{
    public async Task<ScheduleFixturesResult> ScheduleAsync(int competitionId, ScheduleFixturesRequest request)
    {
        await using var db = dbFactory.CreateDbContext();

        var comp = await db.Competition
            .Include(c => c.Stages.OrderBy(s => s.StageOrder))
            .Include(c => c.Participants).ThenInclude(p => p.Player)
            .Include(c => c.Teams)
            .Include(c => c.CourtPairs)
            .Include(c => c.MatchWindows).ThenInclude(w => w.Court)
            .Include(c => c.Divisions)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId);
        if (comp is null) return new ScheduleFixturesResult(0, 0);

        // ── Delete according to mode ─────────────────────────────────────────
        switch (request.DeleteMode)
        {
            case ScheduleDeleteMode.UnscheduledOnly:
            {
                var toDelete = await db.CompetitionFixtures
                    .Where(f => f.CompetitionId == competitionId && f.ScheduledAt == null)
                    .ToListAsync();
                db.CompetitionFixtures.RemoveRange(toDelete);
                if (toDelete.Count > 0) await db.SaveChangesAsync();
                break;
            }
            case ScheduleDeleteMode.AllIncomplete:
            {
                var toDelete = await db.CompetitionFixtures
                    .Where(f => f.CompetitionId == competitionId
                             && f.Status != FixtureStatus.Completed
                             && f.Status != FixtureStatus.AwaitingVerification
                             && f.Status != FixtureStatus.InProgress)
                    .ToListAsync();
                db.CompetitionFixtures.RemoveRange(toDelete);
                if (toDelete.Count > 0) await db.SaveChangesAsync();
                break;
            }
        }

        // ── Reload anything the generator needs ──────────────────────────────
        var courts = comp.HostClubId.HasValue
            ? await db.Courts.Where(c => c.ClubId == comp.HostClubId.Value).OrderBy(c => c.Name).ToListAsync()
            : new List<Court>();

        var existing = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId)
            .ToListAsync();

        var existingRrPairs = existing
            .Where(f => f.Player1Id.HasValue && f.Player2Id.HasValue)
            .Select(f => (Math.Min(f.Player1Id!.Value, f.Player2Id!.Value),
                          Math.Max(f.Player1Id!.Value, f.Player2Id!.Value),
                          f.CompetitionStageId))
            .ToHashSet();

        var existingTeamPairs = existing
            .Where(f => f.HomeTeamId.HasValue && f.AwayTeamId.HasValue)
            .Select(f => (Math.Min(f.HomeTeamId!.Value, f.AwayTeamId!.Value),
                          Math.Max(f.HomeTeamId!.Value, f.AwayTeamId!.Value),
                          f.CompetitionStageId))
            .ToHashSet();

        var existingLabels = existing
            .Where(f => f.FixtureLabel != null)
            .Select(f => ((int?)f.CompetitionStageId, f.FixtureLabel))
            .ToHashSet();

        var activePlayers = comp.Participants
            .Where(p => p.Status is ParticipantStatus.Registered or ParticipantStatus.Confirmed)
            .Select(p => p.PlayerId)
            .ToList();

        bool hasExplicitQF    = comp.Stages.Any(s => s.StageType == StageType.QuarterFinal);
        bool hasExplicitSF    = comp.Stages.Any(s => s.StageType == StageType.SemiFinal);
        bool hasExplicitFinal = comp.Stages.Any(s => s.StageType == StageType.Final);

        bool isTeamComp = comp.CompetitionFormat is CompetitionFormat.Team or CompetitionFormat.TeamMatch
            or CompetitionFormat.Doubles or CompetitionFormat.MixedDoubles;
        bool isTeamMatchScheduling = comp.CompetitionFormat == CompetitionFormat.TeamMatch;
        int n = isTeamComp && comp.Teams.Count >= 2 ? comp.Teams.Count : activePlayers.Count;

        // ── TeamMatch: ensure CourtPairs exist ───────────────────────────────
        var courtPairs = comp.CourtPairs.ToList();
        if (isTeamMatchScheduling && courtPairs.Count == 0 && courts.Count >= 2)
        {
            var autoCreated = new List<CourtPair>();
            for (int i = 0; i + 1 < courts.Count; i += 2)
            {
                autoCreated.Add(new CourtPair
                {
                    CompetitionId = competitionId,
                    Court1Id = courts[i].CourtId,
                    Court2Id = courts[i + 1].CourtId,
                    Name = $"{courts[i].Name} & {courts[i + 1].Name}",
                });
            }
            if (autoCreated.Count > 0)
            {
                db.CourtPairs.AddRange(autoCreated);
                await db.SaveChangesAsync();
                courtPairs = await db.CourtPairs.AsNoTracking()
                    .Where(cp => cp.CompetitionId == competitionId)
                    .OrderBy(cp => cp.Name).ToListAsync();
            }
        }

        // ── Build all valid scheduling slots ─────────────────────────────────
        var startDate = comp.StartDate?.Date ?? DateTime.Today;
        var endDate   = comp.EndDate?.Date ?? startDate.AddDays(365);
        if (endDate < startDate) endDate = startDate.AddDays(365);

        var allSlots = new List<(DateTime time, int? courtId, int? divisionId)>();
        var courtIdToPair = courtPairs.ToDictionary(
            cp => cp.CourtPairId, cp => (cp.Court1Id, cp.Court2Id));

        void AddTeamMatchSlotsForTime(DateTime time, int? windowCourtId, int? divisionId)
        {
            if (courtPairs.Count == 0) { allSlots.Add((time, null, divisionId)); return; }
            var candidates = windowCourtId.HasValue
                ? courtPairs.Where(cp => cp.Court1Id == windowCourtId || cp.Court2Id == windowCourtId)
                : courtPairs;
            foreach (var cp in candidates) allSlots.Add((time, cp.CourtPairId, divisionId));
        }

        var windows = comp.MatchWindows.ToList();
        if (windows.Count > 0)
        {
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                foreach (var w in windows.Where(w => w.DayOfWeek == d.DayOfWeek))
                {
                    var time = d + w.StartTime;
                    var divId = w.CompetitionDivisionId;
                    if (isTeamMatchScheduling) AddTeamMatchSlotsForTime(time, w.CourtId, divId);
                    else if (w.CourtId.HasValue) allSlots.Add((time, w.CourtId, divId));
                    else if (courts.Count > 0) foreach (var court in courts) allSlots.Add((time, court.CourtId, divId));
                    else allSlots.Add((time, null, divId));
                }
            }
        }
        else
        {
            int[] defaultTimes = [9 * 60, 10 * 60 + 30, 12 * 60, 13 * 60 + 30, 15 * 60, 16 * 60 + 30, 18 * 60];
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                foreach (var mins in defaultTimes)
                {
                    var time = d.AddMinutes(mins);
                    if (isTeamMatchScheduling) AddTeamMatchSlotsForTime(time, null, null);
                    else if (courts.Count > 0) foreach (var court in courts) allSlots.Add((time, court.CourtId, null));
                    else allSlots.Add((time, null, null));
                }
            }
        }

        var rng = new Random();
        allSlots = allSlots.OrderBy(s => s.time).ThenBy(_ => rng.Next()).ToList();

        // ── Slot picker with player / team / court busy tracking ─────────────
        var usedSlots = new HashSet<(DateTime, int?)>();
        var courtBusyAt  = new Dictionary<DateTime, HashSet<int>>();
        var playerBusyAt = new Dictionary<DateTime, HashSet<int>>();
        var teamBusyAt   = new Dictionary<DateTime, HashSet<int>>();
        DateTime? earliestAllowedTime = null;

        (DateTime time, int? courtId)? PickSlot(List<int> playerIds, List<int> teamIds, int? requestedDivisionId)
        {
            for (int i = 0; i < allSlots.Count; i++)
            {
                var slot = allSlots[i];
                if (usedSlots.Contains((slot.time, slot.courtId))) continue;
                if (slot.divisionId.HasValue && slot.divisionId != requestedDivisionId) continue;
                if (earliestAllowedTime.HasValue && slot.time <= earliestAllowedTime.Value) continue;

                bool conflict = false;
                if (playerIds.Count > 0 && playerBusyAt.TryGetValue(slot.time, out var pBusy))
                    conflict = playerIds.Any(pid => pBusy.Contains(pid));
                if (!conflict && teamIds.Count > 0 && teamBusyAt.TryGetValue(slot.time, out var tBusy))
                    conflict = teamIds.Any(tid => tBusy.Contains(tid));
                if (!conflict && isTeamMatchScheduling && slot.courtId.HasValue
                    && courtIdToPair.TryGetValue(slot.courtId.Value, out var pairCourts))
                {
                    if (courtBusyAt.TryGetValue(slot.time, out var cBusy)
                        && (cBusy.Contains(pairCourts.Court1Id) || cBusy.Contains(pairCourts.Court2Id)))
                        conflict = true;
                }
                if (conflict) continue;

                usedSlots.Add((slot.time, slot.courtId));
                if (playerIds.Count > 0)
                {
                    if (!playerBusyAt.TryGetValue(slot.time, out var pSet))
                        playerBusyAt[slot.time] = pSet = [];
                    foreach (var pid in playerIds) pSet.Add(pid);
                }
                if (teamIds.Count > 0)
                {
                    if (!teamBusyAt.TryGetValue(slot.time, out var tSet))
                        teamBusyAt[slot.time] = tSet = [];
                    foreach (var tid in teamIds) tSet.Add(tid);
                }
                if (isTeamMatchScheduling && slot.courtId.HasValue
                    && courtIdToPair.TryGetValue(slot.courtId.Value, out var pc))
                {
                    if (!courtBusyAt.TryGetValue(slot.time, out var cSet))
                        courtBusyAt[slot.time] = cSet = [];
                    cSet.Add(pc.Court1Id);
                    cSet.Add(pc.Court2Id);
                }
                return (slot.time, slot.courtId);
            }
            return null;
        }

        int unscheduled = 0;
        var newFixtures = new List<CompetitionFixture>();

        CompetitionFixture? NewScheduledFixture(List<int>? playerIds = null, List<int>? teamIds = null, int? divisionId = null)
        {
            var slot = PickSlot(playerIds ?? [], teamIds ?? [], divisionId);
            if (slot == null) return null;
            var fixture = new CompetitionFixture
            {
                CompetitionId = competitionId,
                ScheduledAt = slot.Value.time,
                Status = FixtureStatus.Scheduled,
            };
            if (isTeamMatchScheduling) fixture.CourtPairId = slot.Value.courtId;
            else fixture.CourtId = slot.Value.courtId;
            return fixture;
        }

        // ── Knockout bucket enumeration ──────────────────────────────────────
        IEnumerable<(string? DivPrefix, int? DivisionId, int N)> EnumerateKnockoutBuckets()
        {
            if (comp.HasDivisions && comp.Divisions.Any())
            {
                foreach (var d in comp.Divisions.OrderBy(x => x.Rank))
                {
                    int bucketN = isTeamComp
                        ? comp.Teams.Count(t => t.CompetitionDivisionId == d.CompetitionDivisionId)
                        : comp.Participants.Count(p =>
                            p.CompetitionDivisionId == d.CompetitionDivisionId &&
                            (p.Status == ParticipantStatus.Registered || p.Status == ParticipantStatus.Confirmed));
                    yield return (d.Name, d.CompetitionDivisionId, bucketN);
                }
            }
            else
            {
                yield return (null, null, n);
            }
        }

        void AddKnockoutFixtures(string fullLabel, string shortLabel, int count, int roundNumber,
                                 string? divPrefix, int? divisionId, int stageId)
        {
            for (int m = 0; m < count; m++)
            {
                var label = divPrefix is null ? $"{fullLabel} {m + 1}" : $"{divPrefix} {shortLabel} {m + 1}";
                if (existingLabels.Contains(((int?)stageId, label))) continue;
                var f = NewScheduledFixture(divisionId: divisionId);
                if (f == null) { unscheduled++; continue; }
                f.CompetitionStageId = stageId;
                f.FixtureLabel = label;
                f.RoundNumber = roundNumber;
                newFixtures.Add(f);
            }
        }

        void AddKnockoutFinal(int roundNumber, string? divPrefix, int? divisionId, int stageId)
        {
            var label = divPrefix is null ? "Final" : $"{divPrefix} Final";
            if (existingLabels.Contains(((int?)stageId, label))) return;
            var f = NewScheduledFixture(divisionId: divisionId);
            if (f == null) { unscheduled++; return; }
            f.CompetitionStageId = stageId;
            f.FixtureLabel = label;
            f.RoundNumber = roundNumber;
            newFixtures.Add(f);
        }

        // ── Generate fixtures per stage ──────────────────────────────────────
        foreach (var stage in comp.Stages)
        {
            int fixtureCountBefore = newFixtures.Count;

            switch (stage.StageType)
            {
                case StageType.RoundRobin:
                {
                    if (isTeamComp && comp.Teams.Count >= 2)
                    {
                        var allMembers = comp.Participants
                            .Where(p => p.TeamId != null && (p.Status is ParticipantStatus.Registered or ParticipantStatus.Confirmed))
                            .ToList();

                        IEnumerable<IGrouping<int?, CompetitionTeam>> teamGroups = comp.HasDivisions
                            ? comp.Teams.GroupBy(t => (int?)t.CompetitionDivisionId)
                            : new[] { comp.Teams.GroupBy(t => (int?)null).First() };

                        foreach (var group in teamGroups)
                        {
                            var groupDivisionId = group.Key;
                            var teamIds = group.Select(t => t.CompetitionTeamId).ToList();
                            if (teamIds.Count < 2) continue;
                            if (teamIds.Count % 2 != 0) teamIds.Add(-1);
                            int total = teamIds.Count;
                            int rounds = total - 1;

                            for (int round = 0; round < rounds; round++)
                            {
                                for (int match = 0; match < total / 2; match++)
                                {
                                    int home = match == 0 ? 0 : ((round + match - 1) % (total - 1)) + 1;
                                    int away = ((round + total - 1 - match) % (total - 1)) + 1;
                                    if (match == 0) { home = 0; away = (round % (total - 1)) + 1; }

                                    int homeTeamId = teamIds[home];
                                    int awayTeamId = teamIds[away];
                                    if (homeTeamId == -1 || awayTeamId == -1) continue;

                                    var teamKey = (Math.Min(homeTeamId, awayTeamId),
                                                   Math.Max(homeTeamId, awayTeamId),
                                                   (int?)stage.CompetitionStageId);
                                    if (existingTeamPairs.Contains(teamKey)) continue;

                                    var fixture = NewScheduledFixture(teamIds: [homeTeamId, awayTeamId], divisionId: groupDivisionId);
                                    if (fixture == null) { unscheduled++; continue; }
                                    fixture.CompetitionStageId = stage.CompetitionStageId;
                                    fixture.HomeTeamId = homeTeamId;
                                    fixture.AwayTeamId = awayTeamId;

                                    if (comp.CompetitionFormat is CompetitionFormat.Doubles or CompetitionFormat.MixedDoubles)
                                    {
                                        var homePair = allMembers.Where(m => m.TeamId == homeTeamId).ToList();
                                        var awayPair = allMembers.Where(m => m.TeamId == awayTeamId).ToList();
                                        if (homePair.Count >= 1) fixture.Player1Id = homePair[0].PlayerId;
                                        if (homePair.Count >= 2) fixture.Player3Id = homePair[1].PlayerId;
                                        if (awayPair.Count >= 1) fixture.Player2Id = awayPair[0].PlayerId;
                                        if (awayPair.Count >= 2) fixture.Player4Id = awayPair[1].PlayerId;
                                    }

                                    newFixtures.Add(fixture);
                                }
                            }
                        }
                    }
                    else
                    {
                        var players = activePlayers.ToList();
                        if (players.Count % 2 != 0) players.Add(-1);
                        int pTotal = players.Count;
                        int pRounds = pTotal - 1;

                        for (int round = 0; round < pRounds; round++)
                        {
                            for (int match = 0; match < pTotal / 2; match++)
                            {
                                int home = match == 0 ? 0 : ((round + match - 1) % (pTotal - 1)) + 1;
                                int away = ((round + pTotal - 1 - match) % (pTotal - 1)) + 1;
                                if (match == 0) { home = 0; away = (round % (pTotal - 1)) + 1; }

                                int p1 = players[home];
                                int p2 = players[away];
                                if (p1 == -1 || p2 == -1) continue;

                                var key = (Math.Min(p1, p2), Math.Max(p1, p2), (int?)stage.CompetitionStageId);
                                if (existingRrPairs.Contains(key)) continue;

                                var pf = NewScheduledFixture(playerIds: [p1, p2]);
                                if (pf == null) { unscheduled++; continue; }
                                pf.CompetitionStageId = stage.CompetitionStageId;
                                pf.Player1Id = p1;
                                pf.Player2Id = p2;
                                newFixtures.Add(pf);
                            }
                        }
                    }
                    break;
                }

                case StageType.Knockout:
                {
                    foreach (var (divPrefix, divisionId, bucketN) in EnumerateKnockoutBuckets())
                    {
                        if (bucketN < 2) continue;
                        bool genQF    = bucketN >= 8 && !hasExplicitQF;
                        bool genSF    = !hasExplicitSF;
                        bool genFinal = !hasExplicitFinal;

                        if (genQF)
                            AddKnockoutFixtures("Quarter-Final", "QF", 4, 1, divPrefix, divisionId, stage.CompetitionStageId);
                        if (genSF)
                            AddKnockoutFixtures("Semi-Final", "SF", 2, genQF ? 2 : 1, divPrefix, divisionId, stage.CompetitionStageId);
                        if (genFinal)
                            AddKnockoutFinal(genQF ? 3 : genSF ? 2 : 1, divPrefix, divisionId, stage.CompetitionStageId);
                    }
                    break;
                }

                case StageType.QuarterFinal:
                {
                    foreach (var (divPrefix, divisionId, _) in EnumerateKnockoutBuckets())
                        AddKnockoutFixtures("Quarter-Final", "QF", 4, 1, divPrefix, divisionId, stage.CompetitionStageId);
                    break;
                }

                case StageType.SemiFinal:
                {
                    foreach (var (divPrefix, divisionId, _) in EnumerateKnockoutBuckets())
                        AddKnockoutFixtures("Semi-Final", "SF", 2, 1, divPrefix, divisionId, stage.CompetitionStageId);
                    break;
                }

                case StageType.Final:
                {
                    foreach (var (divPrefix, divisionId, _) in EnumerateKnockoutBuckets())
                        AddKnockoutFinal(1, divPrefix, divisionId, stage.CompetitionStageId);
                    break;
                }
            }

            var stageFixtures = newFixtures.Skip(fixtureCountBefore).ToList();
            if (stageFixtures.Count > 0)
            {
                var latestInStage = stageFixtures.Max(f => f.ScheduledAt ?? DateTime.MinValue);
                if (!earliestAllowedTime.HasValue || latestInStage > earliestAllowedTime)
                    earliestAllowedTime = latestInStage;
            }
        }

        db.CompetitionFixtures.AddRange(newFixtures);
        await db.SaveChangesAsync();

        return new ScheduleFixturesResult(newFixtures.Count, unscheduled);
    }
}
