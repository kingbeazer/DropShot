using DropShot.Shared.Dtos;

namespace DropShot.Shared.Helpers;

/// <summary>
/// DTO-based port of <c>RubberResolutionService.ComputeLeagueScore</c>. The web
/// service still owns the entity-typed version (used during fixture resolution);
/// this helper runs against the read-only DTOs returned by
/// <c>ICompetitionService.GetCompetitionAsync</c> so the same scoreboard rendering
/// works on both web and MAUI.
/// </summary>
public static class TeamLeagueScoreCalculator
{
    public static (int homeFor, int awayFor, string unitLabel) Compute(
        IEnumerable<RubberDto> rubbers, int homeTeamId, int awayTeamId, LeagueScoringMode mode)
    {
        int homeRubbers = 0, awayRubbers = 0;
        int homeSets = 0, awaySets = 0;
        int homeGames = 0, awayGames = 0;
        foreach (var r in rubbers.Where(r => r.IsComplete))
        {
            if (r.WinnerTeamId == homeTeamId) homeRubbers++;
            else if (r.WinnerTeamId == awayTeamId) awayRubbers++;
            homeSets += r.HomeSetsWon ?? 0;
            awaySets += r.AwaySetsWon ?? 0;
            homeGames += r.HomeGamesTotal ?? 0;
            awayGames += r.AwayGamesTotal ?? 0;
        }
        return mode switch
        {
            LeagueScoringMode.SetsWon => (homeSets, awaySets, "sets"),
            LeagueScoringMode.GamesWon => (homeGames, awayGames, "games"),
            _ => (homeRubbers, awayRubbers, "rubbers"),
        };
    }
}
