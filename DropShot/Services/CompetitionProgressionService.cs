using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Handles automatic bracket progression: when all fixtures in a round are
/// complete, winners are assigned to the next round's fixtures.
/// Covers RoundRobin → QF/SF, QF → SF, and SF → Final transitions.
///
/// For multi-division (league) competitions, progression is scoped per
/// division: each league's fixtures are seeded and advanced independently,
/// using the fixture label prefix set by the auto-scheduler ("{DivisionName} …").
/// </summary>
public static class CompetitionProgressionService
{
    /// <summary>
    /// Call after a CompetitionFixture has been marked Completed and saved.
    /// If all fixtures in the same logical round are now done, the winners
    /// are automatically assigned to the next round's placeholder fixtures.
    /// </summary>
    public static async Task TryAdvanceAsync(MyDbContext db, int competitionId, int completedFixtureId)
    {
        var fixture = await db.CompetitionFixtures
            .Include(f => f.Stage)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == completedFixtureId);

        if (fixture is null || fixture.Status != FixtureStatus.Completed) return;
        if (!fixture.CompetitionStageId.HasValue) return;

        var stage = fixture.Stage;
        if (stage is null) return;

        // Check if this is a team fixture (TeamMatch format)
        bool isTeamFixture = fixture.HomeTeamId.HasValue;

        // For non-team fixtures, require a player winner
        if (!isTeamFixture && !fixture.WinnerPlayerId.HasValue) return;

        var allStages = await db.CompetitionStages
            .Where(s => s.CompetitionId == competitionId)
            .OrderBy(s => s.StageOrder)
            .ToListAsync();

        if (stage.StageType == StageType.RoundRobin)
        {
            if (isTeamFixture)
                await TryAdvanceTeamFromRoundRobinAsync(db, competitionId, allStages);
            else
                await TryAdvanceFromRoundRobinAsync(db, competitionId, allStages);
        }
        else
        {
            if (isTeamFixture)
                await TryAdvanceTeamKnockoutRoundAsync(db, competitionId, fixture, stage, allStages);
            else
                await TryAdvanceKnockoutRoundAsync(db, competitionId, fixture, stage, allStages);
        }
    }

    // ── RoundRobin → first knockout round ────────────────────────────────────

    private static async Task TryAdvanceFromRoundRobinAsync(
        MyDbContext db, int competitionId, List<CompetitionStage> allStages)
    {
        var rrStageIds = allStages
            .Where(s => s.StageType == StageType.RoundRobin)
            .Select(s => s.CompetitionStageId).ToHashSet();

        var rrFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId.HasValue
                     && rrStageIds.Contains(f.CompetitionStageId.Value))
            .ToListAsync();

        bool allDone = rrFixtures.All(f =>
            f.Status == FixtureStatus.Completed ||
            f.Status == FixtureStatus.Walkover   ||
            f.Status == FixtureStatus.Cancelled);

        if (!allDone) return;

        // Find the first knockout stage (QF, SF, or generic Knockout)
        var firstKoStage = allStages
            .Where(s => s.StageType is StageType.Knockout or StageType.QuarterFinal or StageType.SemiFinal)
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault();

        if (firstKoStage is null) return;

        // Get unassigned target fixtures in that stage
        var targetFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId == firstKoStage.CompetitionStageId
                     && (firstKoStage.StageType != StageType.Knockout || f.RoundNumber == 1)
                     && f.Player1Id == null && f.Player2Id == null
                     && f.Status == FixtureStatus.Scheduled)
            .OrderBy(f => f.FixtureLabel)
            .ToListAsync();

        if (!targetFixtures.Any()) return;

        // Compute RR standings
        var activePids = await db.CompetitionParticipants
            .Where(p => p.CompetitionId == competitionId
                     && (p.Status == ParticipantStatus.Registered || p.Status == ParticipantStatus.Confirmed))
            .Select(p => p.PlayerId)
            .ToListAsync();

        var comp = await db.Competition.AsNoTracking().FirstOrDefaultAsync(c => c.CompetitionID == competitionId);
        var scoringMode = comp?.LeagueScoring ?? LeagueScoringMode.WinPoints;

        var pts = activePids.ToDictionary(pid => pid, _ => (Points: 0, Won: 0));
        foreach (var f in rrFixtures.Where(f => f.Status == FixtureStatus.Completed && f.WinnerPlayerId.HasValue))
        {
            var pids = new[] { f.Player1Id, f.Player2Id, f.Player3Id, f.Player4Id }
                .Where(pid => pid.HasValue && pts.ContainsKey(pid.Value))
                .Select(pid => pid!.Value).Distinct();
            foreach (var pid in pids)
            {
                bool win = pid == f.WinnerPlayerId;
                bool isSide1 = pid == f.Player1Id || pid == f.Player3Id;
                int points = scoringMode switch
                {
                    LeagueScoringMode.SetsWon => ParseSetsWon(f.ResultSummary, isSide1),
                    LeagueScoringMode.GamesWon => ParseGamesWon(f.ResultSummary, isSide1),
                    _ => win ? 3 : (!f.WinnerPlayerId.HasValue ? 1 : 0)
                };
                var cur = pts[pid];
                pts[pid] = (cur.Points + points, cur.Won + (win ? 1 : 0));
            }
        }

        // Build head-to-head lookup: (winnerPid, loserPid) pairs
        var h2h = new HashSet<(int winner, int loser)>();
        foreach (var f in rrFixtures.Where(f => f.Status == FixtureStatus.Completed && f.WinnerPlayerId.HasValue))
        {
            var loserIds = new[] { f.Player1Id, f.Player2Id }
                .Where(pid => pid.HasValue && pid.Value != f.WinnerPlayerId!.Value)
                .Select(pid => pid!.Value);
            foreach (var lid in loserIds)
                h2h.Add((f.WinnerPlayerId!.Value, lid));
        }

        var seeded = pts
            .OrderByDescending(kv => kv.Value.Points)
            .ThenByDescending(kv => kv.Value.Won)
            .ThenByDescending(kv => h2h.Count(h => h.winner == kv.Key))
            .Take(targetFixtures.Count * 2)
            .Select(kv => kv.Key)
            .ToList();

        AssignWinners(targetFixtures, seeded);
        await db.SaveChangesAsync();
    }

    // ── QF → SF, SF → Final, or any knockout round → next ───────────────────

    private static async Task TryAdvanceKnockoutRoundAsync(
        MyDbContext db, int competitionId,
        CompetitionFixture completedFixture, CompetitionStage stage,
        List<CompetitionStage> allStages)
    {
        // Collect all fixtures in the same logical "source round"
        IQueryable<CompetitionFixture> sourceQuery = db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId == completedFixture.CompetitionStageId);

        if (stage.StageType == StageType.Knockout)
        {
            // Within a single Knockout stage, round number differentiates QF/SF/Final
            if (!completedFixture.RoundNumber.HasValue) return;
            sourceQuery = sourceQuery.Where(f => f.RoundNumber == completedFixture.RoundNumber);
        }
        // For explicit QF/SF/Final stages, all fixtures in the stage belong to that round

        var sourceFixtures = await sourceQuery.ToListAsync();

        bool allDone = sourceFixtures.All(f =>
            f.Status == FixtureStatus.Completed ||
            f.Status == FixtureStatus.Walkover   ||
            f.Status == FixtureStatus.Cancelled);

        if (!allDone) return;

        var winners = sourceFixtures
            .OrderBy(f => f.FixtureLabel)
            .Where(f => f.WinnerPlayerId.HasValue)
            .Select(f => f.WinnerPlayerId!.Value)
            .ToList();

        if (!winners.Any()) return;

        // Find the unassigned target fixtures in the next round
        List<CompetitionFixture> targetFixtures;

        if (stage.StageType == StageType.Knockout)
        {
            int nextRound = completedFixture.RoundNumber!.Value + 1;
            targetFixtures = await db.CompetitionFixtures
                .Where(f => f.CompetitionId == competitionId
                         && f.CompetitionStageId == completedFixture.CompetitionStageId
                         && f.RoundNumber == nextRound
                         && f.Player1Id == null && f.Player2Id == null
                         && f.Status == FixtureStatus.Scheduled)
                .OrderBy(f => f.FixtureLabel)
                .ToListAsync();
        }
        else
        {
            // Move to the next stage in StageOrder
            var nextStage = allStages
                .Where(s => s.StageOrder > stage.StageOrder)
                .OrderBy(s => s.StageOrder)
                .FirstOrDefault();

            if (nextStage is null) return; // This is the final stage — nothing to advance to

            targetFixtures = await db.CompetitionFixtures
                .Where(f => f.CompetitionId == competitionId
                         && f.CompetitionStageId == nextStage.CompetitionStageId
                         && f.Player1Id == null && f.Player2Id == null
                         && f.Status == FixtureStatus.Scheduled)
                .OrderBy(f => f.FixtureLabel)
                .ToListAsync();
        }

        if (!targetFixtures.Any()) return;

        AssignWinners(targetFixtures, winners);
        await db.SaveChangesAsync();
    }

    // ── Team RoundRobin → first knockout round ────────────────────────────────
    //
    // For multi-division (league) competitions each division's knockout slots
    // are seeded independently, using only that division's RR results.
    // Division membership is inferred from the fixture label prefix set by
    // the auto-scheduler: "{DivisionName} SF 1", "{DivisionName} Final", etc.

    private static async Task TryAdvanceTeamFromRoundRobinAsync(
        MyDbContext db, int competitionId, List<CompetitionStage> allStages)
    {
        var rrStageIds = allStages
            .Where(s => s.StageType == StageType.RoundRobin)
            .Select(s => s.CompetitionStageId).ToHashSet();

        var rrFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId.HasValue
                     && rrStageIds.Contains(f.CompetitionStageId.Value))
            .Include(f => f.Rubbers)
            .ToListAsync();

        bool allDone = rrFixtures.All(f =>
            f.Status == FixtureStatus.Completed ||
            f.Status == FixtureStatus.Walkover   ||
            f.Status == FixtureStatus.Cancelled);

        if (!allDone) return;

        var firstKoStage = allStages
            .Where(s => s.StageType is StageType.Knockout or StageType.QuarterFinal or StageType.SemiFinal)
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault();

        if (firstKoStage is null) return;

        var targetFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId == firstKoStage.CompetitionStageId
                     && (firstKoStage.StageType != StageType.Knockout || f.RoundNumber == 1)
                     && f.HomeTeamId == null && f.AwayTeamId == null
                     && f.Status == FixtureStatus.Scheduled)
            .OrderBy(f => f.FixtureLabel)
            .ToListAsync();

        if (!targetFixtures.Any()) return;

        var allTeams = await db.CompetitionTeams
            .Where(t => t.CompetitionId == competitionId)
            .ToListAsync();

        var divisions = await db.CompetitionDivisions
            .Where(d => d.CompetitionId == competitionId)
            .OrderBy(d => d.Rank)
            .ToListAsync();

        bool hasDivisions = divisions.Any() && allTeams.Any(t => t.CompetitionDivisionId.HasValue);

        if (!hasDivisions)
        {
            // No divisions — seed all teams together (original behaviour)
            var seeded = RankTeamsByRubbers(
                allTeams.Select(t => t.CompetitionTeamId).ToList(), rrFixtures);
            AssignTeamWinners(targetFixtures, seeded.Take(targetFixtures.Count * 2).ToList());
        }
        else
        {
            // Seed each division's knockout fixtures independently using only
            // that division's RR results and label-matched target fixtures.
            foreach (var division in divisions)
            {
                var divTeamIds = allTeams
                    .Where(t => t.CompetitionDivisionId == division.CompetitionDivisionId)
                    .Select(t => t.CompetitionTeamId)
                    .ToList();

                if (!divTeamIds.Any()) continue;

                var divPrefix = division.Name + " ";
                var divTargets = targetFixtures
                    .Where(f => f.FixtureLabel != null &&
                                f.FixtureLabel.StartsWith(divPrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.FixtureLabel)
                    .ToList();

                if (!divTargets.Any()) continue;

                // Only count RR fixtures that involve teams from this division
                var divRrFixtures = rrFixtures
                    .Where(f => (f.HomeTeamId.HasValue && divTeamIds.Contains(f.HomeTeamId.Value)) ||
                                (f.AwayTeamId.HasValue && divTeamIds.Contains(f.AwayTeamId.Value)))
                    .ToList();

                var seeded = RankTeamsByRubbers(divTeamIds, divRrFixtures);
                AssignTeamWinners(divTargets, seeded.Take(divTargets.Count * 2).ToList());
            }
        }

        await db.SaveChangesAsync();
    }

    // ── Team knockout round → next round ────────────────────────────────────
    //
    // For multi-division competitions only the fixtures belonging to the same
    // division as the completed fixture are checked and advanced, so each
    // league's semi-final → final transition fires independently.

    private static async Task TryAdvanceTeamKnockoutRoundAsync(
        MyDbContext db, int competitionId,
        CompetitionFixture completedFixture, CompetitionStage stage,
        List<CompetitionStage> allStages)
    {
        var divisions = await db.CompetitionDivisions
            .Where(d => d.CompetitionId == competitionId)
            .OrderBy(d => d.Rank)
            .ToListAsync();

        // Infer which division this fixture belongs to from its label prefix
        var fixtureDiv = InferDivisionFromLabel(completedFixture.FixtureLabel, divisions);

        IQueryable<CompetitionFixture> sourceQuery = db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId == completedFixture.CompetitionStageId);

        if (stage.StageType == StageType.Knockout)
        {
            if (!completedFixture.RoundNumber.HasValue) return;
            sourceQuery = sourceQuery.Where(f => f.RoundNumber == completedFixture.RoundNumber);
        }

        var sourceFixtures = await sourceQuery.ToListAsync();

        // Narrow to this division only so each league advances independently
        if (fixtureDiv is not null)
        {
            var divPrefix = fixtureDiv.Name + " ";
            sourceFixtures = sourceFixtures
                .Where(f => f.FixtureLabel != null &&
                            f.FixtureLabel.StartsWith(divPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        bool allDone = sourceFixtures.All(f =>
            f.Status == FixtureStatus.Completed ||
            f.Status == FixtureStatus.Walkover   ||
            f.Status == FixtureStatus.Cancelled);

        if (!allDone) return;

        var winners = sourceFixtures
            .OrderBy(f => f.FixtureLabel)
            .Where(f => f.WinnerTeamId.HasValue)
            .Select(f => f.WinnerTeamId!.Value)
            .ToList();

        if (!winners.Any()) return;

        List<CompetitionFixture> targetFixtures;

        if (stage.StageType == StageType.Knockout)
        {
            int nextRound = completedFixture.RoundNumber!.Value + 1;
            targetFixtures = await db.CompetitionFixtures
                .Where(f => f.CompetitionId == competitionId
                         && f.CompetitionStageId == completedFixture.CompetitionStageId
                         && f.RoundNumber == nextRound
                         && f.HomeTeamId == null && f.AwayTeamId == null
                         && f.Status == FixtureStatus.Scheduled)
                .OrderBy(f => f.FixtureLabel)
                .ToListAsync();

            if (fixtureDiv is not null)
            {
                var divPrefix = fixtureDiv.Name + " ";
                targetFixtures = targetFixtures
                    .Where(f => f.FixtureLabel != null &&
                                f.FixtureLabel.StartsWith(divPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
        else
        {
            var nextStage = allStages
                .Where(s => s.StageOrder > stage.StageOrder)
                .OrderBy(s => s.StageOrder)
                .FirstOrDefault();

            if (nextStage is null) return;

            targetFixtures = await db.CompetitionFixtures
                .Where(f => f.CompetitionId == competitionId
                         && f.CompetitionStageId == nextStage.CompetitionStageId
                         && f.HomeTeamId == null && f.AwayTeamId == null
                         && f.Status == FixtureStatus.Scheduled)
                .OrderBy(f => f.FixtureLabel)
                .ToListAsync();

            if (fixtureDiv is not null)
            {
                var divPrefix = fixtureDiv.Name + " ";
                targetFixtures = targetFixtures
                    .Where(f => f.FixtureLabel != null &&
                                f.FixtureLabel.StartsWith(divPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        if (!targetFixtures.Any()) return;

        AssignTeamWinners(targetFixtures, winners);
        await db.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Rank a set of teams by rubbers won (then rubber differential, then rubbers
    /// conceded) using only the provided set of completed RR fixtures.
    /// </summary>
    private static List<int> RankTeamsByRubbers(
        List<int> teamIds, List<CompetitionFixture> rrFixtures)
    {
        var rubbersWon     = teamIds.ToDictionary(tid => tid, _ => 0);
        var rubbersAgainst = teamIds.ToDictionary(tid => tid, _ => 0);

        foreach (var f in rrFixtures.Where(f =>
            f.Status == FixtureStatus.Completed &&
            f.HomeTeamId.HasValue && f.AwayTeamId.HasValue))
        {
            int hid = f.HomeTeamId!.Value;
            int aid = f.AwayTeamId!.Value;
            if (!rubbersWon.ContainsKey(hid) || !rubbersWon.ContainsKey(aid)) continue;

            var completed = f.Rubbers.Where(r => r.IsComplete).ToList();
            int hw = completed.Count(r => r.WinnerTeamId == hid);
            int aw = completed.Count(r => r.WinnerTeamId == aid);

            rubbersWon[hid]     += hw;
            rubbersAgainst[hid] += aw;
            rubbersWon[aid]     += aw;
            rubbersAgainst[aid] += hw;
        }

        return teamIds
            .OrderByDescending(tid => rubbersWon[tid])
            .ThenByDescending(tid => rubbersWon[tid] - rubbersAgainst[tid])
            .ThenBy(tid => rubbersAgainst[tid])
            .ToList();
    }

    /// <summary>
    /// Infer which division a fixture belongs to from its label.
    /// The auto-scheduler prefixes all divisional fixture labels with
    /// "{DivisionName} " (e.g. "League A SF 1"). Longest name wins to
    /// avoid false positives when one division name is a prefix of another.
    /// Returns null for non-divisional or unrecognised labels.
    /// </summary>
    private static CompetitionDivision? InferDivisionFromLabel(
        string? label, List<CompetitionDivision> divisions)
    {
        if (string.IsNullOrEmpty(label) || divisions.Count == 0) return null;

        return divisions
            .Where(d => !string.IsNullOrEmpty(d.Name))
            .OrderByDescending(d => d.Name!.Length)   // longest name first — avoids prefix collisions
            .FirstOrDefault(d =>
                label.StartsWith(d.Name! + " ", StringComparison.OrdinalIgnoreCase));
    }

    // ── Team helper: assign teams to knockout fixtures ──────────────────────

    internal static void AssignTeamWinners(List<CompetitionFixture> targets, List<int> teamIds)
    {
        int n = targets.Count;
        if (teamIds.Count <= n)
        {
            for (int i = 0; i < n; i++)
                targets[i].HomeTeamId = i < teamIds.Count ? teamIds[i] : null;
            return;
        }

        for (int i = 0; i < n; i++)
        {
            int p2Idx = 2 * n - 1 - i;
            targets[i].HomeTeamId = i     < teamIds.Count ? teamIds[i]     : null;
            targets[i].AwayTeamId = p2Idx < teamIds.Count ? teamIds[p2Idx] : null;
        }
    }

    // ── Helper: bracket-pair winners into target fixtures ────────────────────
    // Bracket seeding: seed[i] vs seed[2n-1-i] pairs top with bottom
    // (e.g. 4 players → fixture[0]=(1st,4th), fixture[1]=(2nd,3rd)).

    internal static void AssignWinners(List<CompetitionFixture> targets, List<int> winners)
    {
        int n = targets.Count;
        if (winners.Count <= n)
        {
            // Not enough winners for seeded pairing — assign sequentially
            for (int i = 0; i < n; i++)
                targets[i].Player1Id = i < winners.Count ? winners[i] : null;
            return;
        }

        // Standard seeded pairing: seed[i] vs seed[2n-1-i]
        for (int i = 0; i < n; i++)
        {
            int p2Idx = 2 * n - 1 - i;
            targets[i].Player1Id = i     < winners.Count ? winners[i]     : null;
            targets[i].Player2Id = p2Idx < winners.Count ? winners[p2Idx] : null;
        }
    }

    private static int ParseSetsWon(string? resultSummary, bool isSide1)
    {
        if (string.IsNullOrWhiteSpace(resultSummary)) return 0;
        int count = 0;
        foreach (var set in resultSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = set.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var g1) || !int.TryParse(parts[1], out var g2)) continue;
            if (isSide1 && g1 > g2) count++;
            else if (!isSide1 && g2 > g1) count++;
        }
        return count;
    }

    private static int ParseGamesWon(string? resultSummary, bool isSide1)
    {
        if (string.IsNullOrWhiteSpace(resultSummary)) return 0;
        int total = 0;
        foreach (var set in resultSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = set.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var g1) || !int.TryParse(parts[1], out var g2)) continue;
            total += isSide1 ? g1 : g2;
        }
        return total;
    }
}
