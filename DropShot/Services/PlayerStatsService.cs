using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Computes per-player rubber / set / game aggregates on demand. No stored
/// aggregates — everything reads from <see cref="Rubber"/> joined to the
/// owning fixture / competition. Scoped by the caller to a single division
/// (one competition) or an entire league season (multiple competitions).
/// </summary>
public static class PlayerStatsService
{
    public record PlayerStats(
        int PlayerId,
        int RubbersPlayed,
        int RubbersWon,
        int RubbersLost,
        int SetsWon,
        int SetsAgainst,
        int GamesWon,
        int GamesAgainst,
        int LeaguePoints);

    /// <summary>
    /// Compute per-player stats across every completed rubber in the given set
    /// of competitions, using the league's scoring mode for LeaguePoints.
    /// </summary>
    public static async Task<Dictionary<int, PlayerStats>> ComputeAsync(
        MyDbContext db, IReadOnlyCollection<int> competitionIds, LeagueScoringMode mode)
    {
        if (competitionIds.Count == 0) return new Dictionary<int, PlayerStats>();

        var rubbers = await db.Rubbers
            .AsNoTracking()
            .Include(r => r.Fixture)
            .Where(r => r.IsComplete
                     && r.Fixture.HomeTeamId.HasValue
                     && r.Fixture.AwayTeamId.HasValue
                     && competitionIds.Contains(r.Fixture.CompetitionId))
            .ToListAsync();

        var accum = new Dictionary<int, Mutable>();

        foreach (var r in rubbers)
        {
            int homeTeamId = r.Fixture.HomeTeamId!.Value;
            int awayTeamId = r.Fixture.AwayTeamId!.Value;
            bool homeWon = r.WinnerTeamId == homeTeamId;
            bool awayWon = r.WinnerTeamId == awayTeamId;

            int homeSets = r.HomeSetsWon ?? 0;
            int awaySets = r.AwaySetsWon ?? 0;
            int homeGames = r.HomeGamesTotal ?? 0;
            int awayGames = r.AwayGamesTotal ?? 0;

            int homePoints = mode switch
            {
                LeagueScoringMode.SetsWon  => homeSets,
                LeagueScoringMode.GamesWon => homeGames,
                _ => homeWon ? 3 : (!awayWon ? 1 : 0),
            };
            int awayPoints = mode switch
            {
                LeagueScoringMode.SetsWon  => awaySets,
                LeagueScoringMode.GamesWon => awayGames,
                _ => awayWon ? 3 : (!homeWon ? 1 : 0),
            };

            var homeIds = new[] { r.HomePlayer1Id, r.HomePlayer2Id }.Where(id => id.HasValue).Select(id => id!.Value);
            var awayIds = new[] { r.AwayPlayer1Id, r.AwayPlayer2Id }.Where(id => id.HasValue).Select(id => id!.Value);

            foreach (var pid in homeIds)
                Credit(accum, pid, homeWon, awayWon, homeSets, awaySets, homeGames, awayGames, homePoints);
            foreach (var pid in awayIds)
                Credit(accum, pid, awayWon, homeWon, awaySets, homeSets, awayGames, homeGames, awayPoints);
        }

        return accum.ToDictionary(
            kv => kv.Key,
            kv => new PlayerStats(
                kv.Key,
                kv.Value.RubbersPlayed,
                kv.Value.RubbersWon,
                kv.Value.RubbersLost,
                kv.Value.SetsWon,
                kv.Value.SetsAgainst,
                kv.Value.GamesWon,
                kv.Value.GamesAgainst,
                kv.Value.LeaguePoints));
    }

    private static void Credit(
        Dictionary<int, Mutable> accum, int playerId,
        bool wonRubber, bool opponentWonRubber,
        int setsFor, int setsAgainst, int gamesFor, int gamesAgainst, int points)
    {
        if (!accum.TryGetValue(playerId, out var m))
            accum[playerId] = m = new Mutable();
        m.RubbersPlayed++;
        if (wonRubber) m.RubbersWon++;
        else if (opponentWonRubber) m.RubbersLost++;
        m.SetsWon += setsFor;
        m.SetsAgainst += setsAgainst;
        m.GamesWon += gamesFor;
        m.GamesAgainst += gamesAgainst;
        m.LeaguePoints += points;
    }

    private class Mutable
    {
        public int RubbersPlayed;
        public int RubbersWon;
        public int RubbersLost;
        public int SetsWon;
        public int SetsAgainst;
        public int GamesWon;
        public int GamesAgainst;
        public int LeaguePoints;
    }
}
