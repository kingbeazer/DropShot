using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using Xunit;

namespace DropShot.Tests.Helpers;

public class LadderRatingServiceTests
{
    private const int CompId = 500;
    private const int FxId = 9000;
    private const int PlayerAId = 11;
    private const int PlayerBId = 22;

    private static async Task SeedAsync(
        TestDbContextFactory factory,
        double ratingA = 1000, int matchesA = 0,
        double ratingB = 1000, int matchesB = 0,
        CompetitionFormat format = CompetitionFormat.SinglesLadder,
        double kFactor = 20, int provisional = 10,
        bool useMov = false,
        int? winnerPlayerId = PlayerAId,
        int homeGames = 12, int awayGames = 8)
    {
        using var db = factory.CreateDbContext();
        db.Players.Add(new Player { PlayerId = PlayerAId, DisplayName = "A" });
        db.Players.Add(new Player { PlayerId = PlayerBId, DisplayName = "B" });
        db.Competition.Add(new Competition
        {
            CompetitionID = CompId,
            CompetitionName = "Ladder",
            CompetitionFormat = format,
            LadderKFactor = kFactor,
            LadderStartingRating = 1000,
            LadderProvisionalMatches = provisional,
            LadderUseMarginOfVictory = useMov,
        });
        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = CompId, PlayerId = PlayerAId,
            Status = ParticipantStatus.FullPlayer,
            EloRating = ratingA, MatchesPlayed = matchesA,
            IsProvisional = matchesA < provisional,
        });
        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = CompId, PlayerId = PlayerBId,
            Status = ParticipantStatus.FullPlayer,
            EloRating = ratingB, MatchesPlayed = matchesB,
            IsProvisional = matchesB < provisional,
        });
        db.CompetitionFixtures.Add(new CompetitionFixture
        {
            CompetitionFixtureId = FxId,
            CompetitionId = CompId,
            Player1Id = PlayerAId,
            Player2Id = PlayerBId,
            WinnerPlayerId = winnerPlayerId,
            HomeGamesTotal = homeGames,
            AwayGamesTotal = awayGames,
            Status = FixtureStatus.Completed,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EqualRatedFirstWin_MovesByExpectedDelta()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory);

        using (var db = factory.CreateDbContext())
            await LadderRatingService.ApplyForFinalisedFixtureAsync(db, FxId);

        using var verify = factory.CreateDbContext();
        var pa = verify.CompetitionParticipants.Single(p => p.PlayerId == PlayerAId);
        var pb = verify.CompetitionParticipants.Single(p => p.PlayerId == PlayerBId);

        // Provisional => K=40. Expected score for equal ratings = 0.5.
        // Delta = 40 * (1 - 0.5) = 20.
        Assert.Equal(1020, pa.EloRating, 1);
        Assert.Equal(980, pb.EloRating, 1);
        Assert.Equal(1, pa.MatchesPlayed);
        Assert.Equal(1, pb.MatchesPlayed);
        Assert.True(pa.IsProvisional);
        Assert.True(pb.IsProvisional);
    }

    [Fact]
    public async Task NonLadderFormat_NoOp()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory, format: CompetitionFormat.Singles);

        using (var db = factory.CreateDbContext())
            await LadderRatingService.ApplyForFinalisedFixtureAsync(db, FxId);

        using var verify = factory.CreateDbContext();
        var pa = verify.CompetitionParticipants.Single(p => p.PlayerId == PlayerAId);
        Assert.Equal(1000, pa.EloRating);
        Assert.Equal(0, pa.MatchesPlayed);
    }

    [Fact]
    public async Task EleventhMatch_FlipsOutOfProvisional_AndUsesBaseK()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory, matchesA: 10, matchesB: 10);

        using (var db = factory.CreateDbContext())
            await LadderRatingService.ApplyForFinalisedFixtureAsync(db, FxId);

        using var verify = factory.CreateDbContext();
        var pa = verify.CompetitionParticipants.Single(p => p.PlayerId == PlayerAId);
        var pb = verify.CompetitionParticipants.Single(p => p.PlayerId == PlayerBId);

        // K=20 (post-provisional). Delta = 20 * 0.5 = 10.
        Assert.Equal(1010, pa.EloRating, 1);
        Assert.Equal(990, pb.EloRating, 1);
        Assert.False(pa.IsProvisional);
        Assert.False(pb.IsProvisional);
    }

    [Fact]
    public async Task FixtureAlreadyApplied_Idempotent()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory);

        using (var db = factory.CreateDbContext())
            await LadderRatingService.ApplyForFinalisedFixtureAsync(db, FxId);
        using (var db = factory.CreateDbContext())
            await LadderRatingService.ApplyForFinalisedFixtureAsync(db, FxId);

        using var verify = factory.CreateDbContext();
        var pa = verify.CompetitionParticipants.Single(p => p.PlayerId == PlayerAId);
        // Should only have moved once.
        Assert.Equal(1020, pa.EloRating, 1);
        Assert.Equal(1, pa.MatchesPlayed);
    }

    [Fact]
    public async Task StoresBeforeAndAfterOnFixture()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory, ratingA: 1100, ratingB: 900);

        using (var db = factory.CreateDbContext())
            await LadderRatingService.ApplyForFinalisedFixtureAsync(db, FxId);

        using var verify = factory.CreateDbContext();
        var fx = verify.CompetitionFixtures.Single(f => f.CompetitionFixtureId == FxId);
        Assert.Equal(1100, fx.Player1RatingBefore);
        Assert.Equal(900, fx.Player2RatingBefore);
        Assert.NotNull(fx.Player1RatingAfter);
        Assert.NotNull(fx.Player2RatingAfter);
        Assert.True(fx.Player1RatingAfter! > fx.Player1RatingBefore);
        Assert.True(fx.Player2RatingAfter! < fx.Player2RatingBefore);
    }
}
