using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Handles automatic bracket progression: when all fixtures in a round are
/// complete, winners are assigned to the next round's fixtures.
/// Covers RoundRobin → QF/SF, QF → SF, and SF → Final transitions.
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

        // Check if this is a team fixture (MixedTeam format)
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
            .Include(f => f.TeamMatchSets)
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

        // Rank teams by sets won
        var teamIds = await db.CompetitionTeams
            .Where(t => t.CompetitionId == competitionId)
            .Select(t => t.CompetitionTeamId)
            .ToListAsync();

        var setsWon = teamIds.ToDictionary(tid => tid, _ => 0);
        var setsAgainst = teamIds.ToDictionary(tid => tid, _ => 0);

        foreach (var f in rrFixtures.Where(f => f.Status == FixtureStatus.Completed
                    && f.HomeTeamId.HasValue && f.AwayTeamId.HasValue))
        {
            int hid = f.HomeTeamId!.Value;
            int aid = f.AwayTeamId!.Value;
            if (!setsWon.ContainsKey(hid) || !setsWon.ContainsKey(aid)) continue;

            var completed = f.TeamMatchSets.Where(s => s.IsComplete).ToList();
            int hw = completed.Count(s => s.WinnerTeamId == hid);
            int aw = completed.Count(s => s.WinnerTeamId == aid);

            setsWon[hid] += hw;
            setsAgainst[hid] += aw;
            setsWon[aid] += aw;
            setsAgainst[aid] += hw;
        }

        var seeded = teamIds
            .OrderByDescending(tid => setsWon[tid])
            .ThenByDescending(tid => setsWon[tid] - setsAgainst[tid])
            .ThenBy(tid => setsAgainst[tid])
            .Take(targetFixtures.Count * 2)
            .ToList();

        AssignTeamWinners(targetFixtures, seeded);

        // Create TeamMatchSet rows for the newly assigned knockout fixtures
        await CreateTeamMatchSetsForFixtures(db, competitionId, targetFixtures);

        await db.SaveChangesAsync();
    }

    // ── Team knockout round → next round ────────────────────────────────────

    private static async Task TryAdvanceTeamKnockoutRoundAsync(
        MyDbContext db, int competitionId,
        CompetitionFixture completedFixture, CompetitionStage stage,
        List<CompetitionStage> allStages)
    {
        IQueryable<CompetitionFixture> sourceQuery = db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId == completedFixture.CompetitionStageId);

        if (stage.StageType == StageType.Knockout)
        {
            if (!completedFixture.RoundNumber.HasValue) return;
            sourceQuery = sourceQuery.Where(f => f.RoundNumber == completedFixture.RoundNumber);
        }

        var sourceFixtures = await sourceQuery.ToListAsync();

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
        }

        if (!targetFixtures.Any()) return;

        AssignTeamWinners(targetFixtures, winners);

        await CreateTeamMatchSetsForFixtures(db, competitionId, targetFixtures);

        await db.SaveChangesAsync();
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
            targets[i].HomeTeamId = i < teamIds.Count ? teamIds[i] : null;
            targets[i].AwayTeamId = p2Idx < teamIds.Count ? teamIds[p2Idx] : null;
        }
    }

    // ── Helper: create TeamMatchSet rows for newly assigned fixtures ────────

    private static async Task CreateTeamMatchSetsForFixtures(
        MyDbContext db, int competitionId, List<CompetitionFixture> fixtures)
    {
        foreach (var fx in fixtures.Where(f => f.HomeTeamId.HasValue && f.AwayTeamId.HasValue))
        {
            var homeMembers = await db.CompetitionParticipants
                .Where(cp => cp.CompetitionId == competitionId && cp.TeamId == fx.HomeTeamId
                    && (cp.Status == ParticipantStatus.Registered || cp.Status == ParticipantStatus.Confirmed))
                .Include(cp => cp.Player)
                .ToListAsync();

            var awayMembers = await db.CompetitionParticipants
                .Where(cp => cp.CompetitionId == competitionId && cp.TeamId == fx.AwayTeamId
                    && (cp.Status == ParticipantStatus.Registered || cp.Status == ParticipantStatus.Confirmed))
                .Include(cp => cp.Player)
                .ToListAsync();

            if (homeMembers.Count == 4 && awayMembers.Count == 4
                && homeMembers.All(m => m.Grade != null && m.Player?.Sex != null)
                && awayMembers.All(m => m.Grade != null && m.Player?.Sex != null))
            {
                var sets = TeamMatchService.CreateSetsForFixture(fx.CompetitionFixtureId, homeMembers, awayMembers);
                db.TeamMatchSets.AddRange(sets);
            }
        }
    }

    // ── Helper: bracket-pair winners into target fixtures ────────────────────
    // source[0], source[1] → target[0].Player1, target[0].Player2
    // source[2], source[3] → target[1].Player1, target[1].Player2  etc.

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
