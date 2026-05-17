using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Admin-only test fixture: generates synthetic SinglesLadder activity so an
/// admin can see ratings, decay, and the activity feed in action without
/// waiting weeks of real play.
///
/// Destructive — wipes all fixtures and decay events for the target ladder
/// and resets every participant's rating bookkeeping to the competition's
/// starting state before simulating. The endpoint that calls this is gated
/// to SuperAdmin only.
/// </summary>
public static class LadderSimulationService
{
    public sealed record SimulationResult(
        int Participants, int ActivePlayers, int IdlePlayers,
        int FixturesGenerated, int DecayEventsGenerated);

    public static async Task<SimulationResult> SimulateAsync(
        MyDbContext db, int competitionId, int weeks, int? seed, CancellationToken ct = default)
    {
        if (weeks < 1 || weeks > 26)
            throw new ArgumentOutOfRangeException(nameof(weeks), "weeks must be 1..26");

        var comp = await db.Competition.FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new InvalidOperationException("Competition not found.");
        if (comp.CompetitionFormat != CompetitionFormat.SinglesLadder)
            throw new InvalidOperationException("Simulation is only valid for SinglesLadder competitions.");

        var participants = await db.CompetitionParticipants
            .Where(p => p.CompetitionId == competitionId && p.Status == ParticipantStatus.FullPlayer)
            .Include(p => p.Player)
            .ToListAsync(ct);
        if (participants.Count < 4)
            throw new InvalidOperationException("Need at least 4 FullPlayer participants to simulate.");

        var rnd = seed.HasValue ? new Random(seed.Value) : new Random();

        // ── Wipe any prior fixtures + decay events for this ladder ─────────
        var existingFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId)
            .ToListAsync(ct);
        db.CompetitionFixtures.RemoveRange(existingFixtures);

        var existingDecays = await db.LadderInactivityDecays
            .Where(d => d.CompetitionId == competitionId)
            .ToListAsync(ct);
        db.LadderInactivityDecays.RemoveRange(existingDecays);

        // Reset participant state to the starting line.
        foreach (var p in participants)
        {
            p.EloRating = comp.LadderStartingRating;
            p.MatchesPlayed = 0;
            p.IsProvisional = true;
            p.LastMatchAt = null;
            p.LastDecayAppliedAt = null;
            p.LastInactivityWarningAt = null;
        }
        await db.SaveChangesAsync(ct);

        // ── Pick active vs idle (deterministic via seed) ───────────────────
        var shuffled = participants.OrderBy(_ => rnd.Next()).ToList();
        int idleCount = Math.Max(1, shuffled.Count * 3 / 10);   // ~30 % idle
        var idle = shuffled.Take(idleCount).ToList();
        var active = shuffled.Skip(idleCount).ToList();

        var now = DateTime.UtcNow;
        var simStart = now.AddDays(-weeks * 7);

        // Idle players "last played" two weeks before the sim started — gives
        // their decay clock something to chew on while sweeps run.
        foreach (var p in idle) p.LastMatchAt = simStart.AddDays(-14);

        // ── Generate match fixtures across the window ──────────────────────
        // Aim for roughly 1.5 matches per active player per week.
        int matchesAdded = 0;
        for (int day = 0; day < weeks * 7; day++)
        {
            var dayStart = simStart.AddDays(day);

            int target = (active.Count * 3) / 14;             // ~1.5/wk per player ÷ 2 (each match = 2 players)
            int matchesToday = Math.Max(0, target + rnd.Next(-1, 2));

            for (int i = 0; i < matchesToday; i++)
            {
                var p1 = active[rnd.Next(active.Count)];
                var p2 = active[rnd.Next(active.Count)];
                int tries = 0;
                while (p2.PlayerId == p1.PlayerId && tries++ < 5)
                    p2 = active[rnd.Next(active.Count)];
                if (p2.PlayerId == p1.PlayerId) continue;

                double expA = EloCalculator.ExpectedScore(p1.EloRating, p2.EloRating);
                bool p1Wins = rnd.NextDouble() < expA;

                int wSets = 2;
                int lSets = rnd.Next(0, 2); // 0 or 1
                int wGames = wSets * 6 + rnd.Next(0, 4);
                int lGames = lSets * 6 + rnd.Next(0, 7);

                int homeSets = p1Wins ? wSets : lSets;
                int awaySets = p1Wins ? lSets : wSets;
                int homeGames = p1Wins ? wGames : lGames;
                int awayGames = p1Wins ? lGames : wGames;

                double? mov = comp.LadderUseMarginOfVictory && homeGames != awayGames
                    ? EloCalculator.MarginOfVictoryMultiplier(homeGames, awayGames, p1.EloRating, p2.EloRating)
                    : null;

                double kA = p1.MatchesPlayed < comp.LadderProvisionalMatches
                    ? comp.LadderKFactor * 2 : comp.LadderKFactor;
                double kB = p2.MatchesPlayed < comp.LadderProvisionalMatches
                    ? comp.LadderKFactor * 2 : comp.LadderKFactor;

                double scoreA = p1Wins ? 1.0 : 0.0;
                double newA = EloCalculator.UpdateRating(p1.EloRating, expA, scoreA, kA, mov);
                double newB = EloCalculator.UpdateRating(p2.EloRating, 1.0 - expA, 1.0 - scoreA, kB, mov);

                var fx = new CompetitionFixture
                {
                    CompetitionId = competitionId,
                    Player1Id = p1.PlayerId,
                    Player2Id = p2.PlayerId,
                    Status = FixtureStatus.Completed,
                    ScheduledAt = dayStart,
                    CompletedAt = dayStart.AddHours(1),
                    WinnerPlayerId = p1Wins ? p1.PlayerId : p2.PlayerId,
                    HomeSetsWon = homeSets,
                    AwaySetsWon = awaySets,
                    HomeGamesTotal = homeGames,
                    AwayGamesTotal = awayGames,
                    ResultSummary = $"[sim] {homeSets}-{awaySets} ({homeGames}-{awayGames} games)",
                    Player1RatingBefore = p1.EloRating,
                    Player1RatingAfter = newA,
                    Player2RatingBefore = p2.EloRating,
                    Player2RatingAfter = newB,
                };
                db.CompetitionFixtures.Add(fx);

                p1.EloRating = newA;
                p1.MatchesPlayed++;
                p1.LastMatchAt = fx.CompletedAt;
                p1.IsProvisional = p1.MatchesPlayed < comp.LadderProvisionalMatches;

                p2.EloRating = newB;
                p2.MatchesPlayed++;
                p2.LastMatchAt = fx.CompletedAt;
                p2.IsProvisional = p2.MatchesPlayed < comp.LadderProvisionalMatches;

                matchesAdded++;
            }
        }
        await db.SaveChangesAsync(ct);

        // ── Run weekly inactivity sweeps over the simulated window ─────────
        // RunSweepAsync gates on Competition.IsStarted; flip it on if the
        // admin hasn't pressed Start yet, otherwise the sweep is a no-op.
        bool flippedStarted = false;
        if (!comp.IsStarted)
        {
            comp.IsStarted = true;
            flippedStarted = true;
            await db.SaveChangesAsync(ct);
        }

        int decayCount = 0;
        for (var sweep = simStart.AddDays(7); sweep <= now; sweep = sweep.AddDays(7))
        {
            var r = await LadderInactivityService.RunSweepAsync(db, sweep, null, ct);
            decayCount += r.DecayEventsApplied;
        }
        // One more sweep at "now" to catch any decay events that fall between
        // the last 7-day boundary and the simulation end.
        if (now > simStart.AddDays(((weeks * 7) / 7) * 7))
        {
            var r = await LadderInactivityService.RunSweepAsync(db, now, null, ct);
            decayCount += r.DecayEventsApplied;
        }

        if (flippedStarted)
        {
            // Leave IsStarted true — simulation is meaningless otherwise, and
            // any subsequent real play needs it on too.
        }

        return new SimulationResult(
            Participants: participants.Count,
            ActivePlayers: active.Count,
            IdlePlayers: idle.Count,
            FixturesGenerated: matchesAdded,
            DecayEventsGenerated: decayCount);
    }
}
