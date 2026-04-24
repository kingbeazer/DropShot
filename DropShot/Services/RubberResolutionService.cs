using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public class RubberResolutionException : Exception
{
    public RubberResolutionException(string message) : base(message) { }
}

public class RubberResolutionService(ICompetitionRubberTemplateProvider templateProvider)
{
    /// <summary>
    /// Creates Rubber rows for a fixture by resolving each rubber definition's roles
    /// against the home and away team memberships. Called lazily when a fixture is first
    /// opened for scoring, so late team edits before match day don't corrupt in-progress
    /// scorecards.
    /// </summary>
    public async Task EnsureRubbersAsync(MyDbContext db, int fixtureId)
    {
        var fixture = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .Include(f => f.Rubbers)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId);

        if (fixture is null)
            throw new RubberResolutionException($"Fixture {fixtureId} not found.");
        if (fixture.Rubbers.Count > 0) return;
        if (!fixture.HomeTeamId.HasValue || !fixture.AwayTeamId.HasValue) return;

        var template = await templateProvider.GetAsync(db, fixture.CompetitionId);
        if (template is null || template.Count == 0) return;

        var homeMembers = await LoadTeamMembers(db, fixture.CompetitionId, fixture.HomeTeamId.Value);
        var awayMembers = await LoadTeamMembers(db, fixture.CompetitionId, fixture.AwayTeamId.Value);

        foreach (var def in template)
        {
            var rubber = new Rubber
            {
                CompetitionFixtureId = fixtureId,
                Order = def.Order,
                Name = def.Name,
                CourtNumber = def.CourtNumber,
                HomeRoles = def.HomeRoles,
                AwayRoles = def.AwayRoles,
            };

            var homeIds = ResolveRoles(def.HomeRoles, homeMembers, "home", def.Name);
            var awayIds = ResolveRoles(def.AwayRoles, awayMembers, "away", def.Name);

            rubber.HomePlayer1Id = homeIds.ElementAtOrDefault(0);
            rubber.HomePlayer2Id = homeIds.ElementAtOrDefault(1);
            rubber.AwayPlayer1Id = awayIds.ElementAtOrDefault(0);
            rubber.AwayPlayer2Id = awayIds.ElementAtOrDefault(1);

            db.Rubbers.Add(rubber);
        }

        // Do NOT advance status here — rubber rows are created lazily when the
        // team-match page first loads, which is before any score is entered.
        // Status is set to InProgress only when a rubber score is actually saved.
        await db.SaveChangesAsync();
    }

    public static (int homeScore, int awayScore) ComputeScore(
        IEnumerable<Rubber> rubbers, int homeTeamId, int awayTeamId)
    {
        int home = 0, away = 0;
        foreach (var r in rubbers.Where(r => r.IsComplete))
        {
            if (r.WinnerTeamId == homeTeamId) home++;
            else if (r.WinnerTeamId == awayTeamId) away++;
        }
        return (home, away);
    }

    /// <summary>
    /// Returns the for/against metric that matches the competition's LeagueScoring
    /// mode — rubbers (default / WinPoints), sets, or total games. The label is
    /// suitable for UI use ("rubbers" / "sets" / "games").
    /// </summary>
    public static (int homeFor, int awayFor, string unitLabel) ComputeLeagueScore(
        IEnumerable<Rubber> rubbers, int homeTeamId, int awayTeamId, LeagueScoringMode mode)
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
            LeagueScoringMode.SetsWon  => (homeSets, awaySets, "sets"),
            LeagueScoringMode.GamesWon => (homeGames, awayGames, "games"),
            _                          => (homeRubbers, awayRubbers, "rubbers"),
        };
    }

    public static bool AllComplete(IEnumerable<Rubber> rubbers)
        => rubbers.All(r => r.IsComplete);

    private static async Task<List<CompetitionParticipant>> LoadTeamMembers(
        MyDbContext db, int competitionId, int teamId)
    {
        return await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == competitionId && cp.TeamId == teamId
                && (cp.Status == ParticipantStatus.Registered || cp.Status == ParticipantStatus.Confirmed))
            .Include(cp => cp.Player)
            .ToListAsync();
    }

    private static List<int> ResolveRoles(
        IReadOnlyList<string> roles, List<CompetitionParticipant> members, string side, string rubberName)
    {
        var ids = new List<int>();
        foreach (var role in roles)
        {
            var matches = members.Where(m => m.Role == role).ToList();
            if (matches.Count == 0)
                throw new RubberResolutionException(
                    $"The {side} team is missing a player with role '{role}' for rubber '{rubberName}'.");
            if (matches.Count > 1)
                throw new RubberResolutionException(
                    $"The {side} team has more than one player with role '{role}'.");
            ids.Add(matches[0].PlayerId);
        }
        return ids;
    }
}
