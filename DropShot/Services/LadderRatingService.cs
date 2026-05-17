using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Applies Elo rating updates for singles-ladder competitions when a fixture
/// is finalised. Called from <c>WebMatchScoringService.FinaliseLiveFixtureAsync</c>
/// right after the fixture is saved as Completed.
///
/// No-ops for any competition whose format is not <see cref="CompetitionFormat.SinglesLadder"/>,
/// so the call site can invoke it on every finalised fixture without branching.
///
/// Idempotent: if the fixture already has rating-after values set, the call
/// is skipped — protects against duplicate finalise requests.
/// </summary>
public static class LadderRatingService
{
    public static async Task ApplyForFinalisedFixtureAsync(
        MyDbContext db, int fixtureId, CancellationToken ct = default)
    {
        var fx = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct);

        if (fx is null) return;
        if (fx.Competition.CompetitionFormat != CompetitionFormat.SinglesLadder) return;
        if (fx.Player1RatingAfter.HasValue) return; // idempotency guard
        if (fx.Player1Id is null || fx.Player2Id is null) return;
        if (fx.WinnerPlayerId is null) return;

        int competitionId = fx.CompetitionId;
        int p1Id = fx.Player1Id.Value;
        int p2Id = fx.Player2Id.Value;

        var participants = await db.CompetitionParticipants
            .Where(p => p.CompetitionId == competitionId
                        && (p.PlayerId == p1Id || p.PlayerId == p2Id))
            .ToListAsync(ct);

        var pa = participants.FirstOrDefault(p => p.PlayerId == p1Id);
        var pb = participants.FirstOrDefault(p => p.PlayerId == p2Id);
        if (pa is null || pb is null) return;

        var comp = fx.Competition;
        double ratingA = pa.EloRating;
        double ratingB = pb.EloRating;
        double scoreA = fx.WinnerPlayerId == p1Id ? 1.0 : 0.0;
        double scoreB = 1.0 - scoreA;

        double kA = pa.MatchesPlayed < comp.LadderProvisionalMatches
            ? comp.LadderKFactor * 2.0
            : comp.LadderKFactor;
        double kB = pb.MatchesPlayed < comp.LadderProvisionalMatches
            ? comp.LadderKFactor * 2.0
            : comp.LadderKFactor;

        double expectedA = EloCalculator.ExpectedScore(ratingA, ratingB);
        double expectedB = 1.0 - expectedA;

        double? mov = null;
        if (comp.LadderUseMarginOfVictory
            && fx.HomeGamesTotal.HasValue
            && fx.AwayGamesTotal.HasValue
            && fx.HomeGamesTotal.Value != fx.AwayGamesTotal.Value)
        {
            mov = EloCalculator.MarginOfVictoryMultiplier(
                fx.HomeGamesTotal.Value, fx.AwayGamesTotal.Value, ratingA, ratingB);
        }

        double newA = EloCalculator.UpdateRating(ratingA, expectedA, scoreA, kA, mov);
        double newB = EloCalculator.UpdateRating(ratingB, expectedB, scoreB, kB, mov);

        fx.Player1RatingBefore = ratingA;
        fx.Player1RatingAfter = newA;
        fx.Player2RatingBefore = ratingB;
        fx.Player2RatingAfter = newB;

        var now = DateTime.UtcNow;
        pa.EloRating = newA;
        pa.MatchesPlayed += 1;
        pa.LastMatchAt = now;
        pa.IsProvisional = pa.MatchesPlayed < comp.LadderProvisionalMatches;

        pb.EloRating = newB;
        pb.MatchesPlayed += 1;
        pb.LastMatchAt = now;
        pb.IsProvisional = pb.MatchesPlayed < comp.LadderProvisionalMatches;

        await db.SaveChangesAsync(ct);
    }
}
