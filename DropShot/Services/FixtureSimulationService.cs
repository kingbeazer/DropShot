using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Generates plausible random results for round-robin fixtures. Intended as a
/// super-admin test utility — respects competition match-format rules so the
/// saved scores pass the same validation the score-entry dialogs enforce.
/// </summary>
public class FixtureSimulationService(RubberResolutionService rubberResolver)
{
    private static readonly Random _rng = new();

    public record Outcome(int FixturesSimulated, int FixturesSkipped, List<string> Errors);

    public async Task<Outcome> SimulateRoundRobinAsync(MyDbContext db, int competitionId)
    {
        var fixtures = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .Include(f => f.Stage)
            .Where(f => f.CompetitionId == competitionId
                        && f.Stage != null
                        && f.Stage!.StageType == StageType.RoundRobin
                        && f.Status != FixtureStatus.Completed
                        && f.Status != FixtureStatus.Cancelled
                        && f.Status != FixtureStatus.Walkover
                        && f.Status != FixtureStatus.AwaitingVerification)
            .ToListAsync();

        int done = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var fx in fixtures)
        {
            try
            {
                if (fx.HomeTeamId.HasValue && fx.AwayTeamId.HasValue)
                {
                    await SimulateTeamFixtureAsync(db, fx);
                    done++;
                }
                else if (fx.Player1Id.HasValue && fx.Player2Id.HasValue)
                {
                    await SimulatePlayerFixtureAsync(db, fx);
                    done++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (RubberResolutionException ex)
            {
                skipped++;
                errors.Add($"Fixture {fx.CompetitionFixtureId}: {ex.Message}");
            }
        }

        return new Outcome(done, skipped, errors);
    }

    private async Task SimulateTeamFixtureAsync(MyDbContext db, CompetitionFixture fx)
    {
        await rubberResolver.EnsureRubbersAsync(db, fx.CompetitionFixtureId);

        var rubbers = await db.Rubbers
            .Where(r => r.CompetitionFixtureId == fx.CompetitionFixtureId)
            .ToListAsync();

        foreach (var rub in rubbers.Where(r => !r.IsComplete))
            FillRubber(rub, fx);

        await db.SaveChangesAsync();

        var allRubbers = await db.Rubbers
            .Where(r => r.CompetitionFixtureId == fx.CompetitionFixtureId)
            .ToListAsync();

        if (!RubberResolutionService.AllComplete(allRubbers)) return;

        var (homeScore, awayScore) = RubberResolutionService.ComputeScore(
            allRubbers, fx.HomeTeamId!.Value, fx.AwayTeamId!.Value);

        fx.Status = FixtureStatus.Completed;
        fx.CompletedAt = DateTime.UtcNow;
        fx.ResultSummary = $"{homeScore}-{awayScore}";
        fx.WinnerTeamId = homeScore > awayScore ? fx.HomeTeamId
                         : awayScore > homeScore ? fx.AwayTeamId
                         : null;
        fx.VerificationToken = null;
        await db.SaveChangesAsync();

        await CompetitionProgressionService.TryAdvanceAsync(db, fx.CompetitionId, fx.CompetitionFixtureId);
    }

    private static async Task SimulatePlayerFixtureAsync(MyDbContext db, CompetitionFixture fx)
    {
        var comp = fx.Competition!;
        var (bestOf, gamesPerSet, setWinMode, isFixedSets) = GetFormat(comp);

        var sets = GenerateMatchSets(bestOf, gamesPerSet, setWinMode, isFixedSets);
        int side1Sets = sets.Count(s => s.Side1 > s.Side2);
        int side2Sets = sets.Count(s => s.Side2 > s.Side1);

        fx.ResultSummary = string.Join(" ", sets.Select(s => $"{s.Side1}\u2013{s.Side2}"));
        fx.WinnerPlayerId = side1Sets > side2Sets ? fx.Player1Id
                           : side2Sets > side1Sets ? fx.Player2Id
                           : null;
        fx.Status = FixtureStatus.Completed;
        fx.CompletedAt = DateTime.UtcNow;
        fx.VerificationToken = null;

        await db.SaveChangesAsync();
        await CompetitionProgressionService.TryAdvanceAsync(db, fx.CompetitionId, fx.CompetitionFixtureId);
    }

    private static void FillRubber(Rubber rub, CompetitionFixture fx)
    {
        var (bestOf, gamesPerSet, setWinMode, isFixedSets) = GetFormat(fx.Competition!);
        var sets = GenerateMatchSets(bestOf, gamesPerSet, setWinMode, isFixedSets);

        int homeSets = sets.Count(s => s.Side1 > s.Side2);
        int awaySets = sets.Count(s => s.Side2 > s.Side1);
        int homeGames = sets.Sum(s => s.Side1);
        int awayGames = sets.Sum(s => s.Side2);
        var last = sets[^1];

        rub.IsComplete = true;
        rub.HomeSetsWon = homeSets;
        rub.AwaySetsWon = awaySets;
        rub.HomeGamesTotal = homeGames;
        rub.AwayGamesTotal = awayGames;
        rub.HomeGames = last.Side1;
        rub.AwayGames = last.Side2;
        rub.WinnerTeamId = homeSets > awaySets ? fx.HomeTeamId
                          : awaySets > homeSets ? fx.AwayTeamId
                          : null;
        rub.SavedMatchId = null;
    }

    private static (int bestOf, int gamesPerSet, SetWinMode mode, bool isFixedSets) GetFormat(Competition comp)
    {
        bool isFixedSets = comp.MatchFormat == MatchFormatType.FixedSets;
        int bestOf = isFixedSets ? Math.Max(1, comp.NumberOfSets) : Math.Max(1, comp.BestOf);
        return (bestOf, Math.Max(1, comp.GamesPerSet), comp.SetWinMode, isFixedSets);
    }

    private record SetScore(int Side1, int Side2);

    private static List<SetScore> GenerateMatchSets(int bestOf, int gamesPerSet, SetWinMode mode, bool isFixedSets)
    {
        var sets = new List<SetScore>();
        int target = bestOf / 2 + 1;
        int s1 = 0, s2 = 0;
        for (int i = 0; i < bestOf; i++)
        {
            if (!isFixedSets && (s1 >= target || s2 >= target)) break;
            bool side1Wins = _rng.Next(2) == 0;
            var (winG, loseG) = RandomSetScore(gamesPerSet, mode);
            if (side1Wins) { sets.Add(new SetScore(winG, loseG)); s1++; }
            else           { sets.Add(new SetScore(loseG, winG)); s2++; }
        }
        return sets;
    }

    private static (int winner, int loser) RandomSetScore(int gamesPerSet, SetWinMode mode)
    {
        // A safe valid score for both WinBy2 and FirstTo: winner reaches exactly
        // gamesPerSet with loser at most gamesPerSet-2. Matches the strictest
        // branch of the score-entry validators.
        int maxLoser = Math.Max(0, gamesPerSet - 2);
        int loser = _rng.Next(0, maxLoser + 1);
        return (gamesPerSet, loser);
    }
}
