using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using Xunit;
using DecayRunResult = DropShot.Services.LadderInactivityService.DecayRunResult;

namespace DropShot.Tests.Helpers;

public class LadderInactivityServiceTests
{
    private const int CompId = 700;
    private const int PlayerAId = 11;
    private const int PlayerBId = 22;
    private static readonly DateTime BaseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task SeedAsync(
        TestDbContextFactory factory,
        DateTime registeredAt,
        DateTime? lastMatchAt,
        double rating = 1100,
        DateTime? lastDecayAt = null,
        DateTime? lastWarnAt = null,
        CompetitionFormat format = CompetitionFormat.SinglesLadder,
        bool isStarted = true,
        bool isArchived = false,
        double startingRating = 1000,
        int playerId = PlayerAId,
        string email = "p@example.com")
    {
        using var db = factory.CreateDbContext();
        if (!db.Players.Any(p => p.PlayerId == playerId))
        {
            db.Players.Add(new Player
            {
                PlayerId = playerId,
                DisplayName = $"P{playerId}",
                Email = email,
            });
        }
        if (!db.Competition.Any(c => c.CompetitionID == CompId))
        {
            db.Competition.Add(new Competition
            {
                CompetitionID = CompId,
                CompetitionName = "Ladder",
                CompetitionFormat = format,
                IsStarted = isStarted,
                IsArchived = isArchived,
                LadderStartingRating = startingRating,
            });
        }
        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = CompId,
            PlayerId = playerId,
            Status = ParticipantStatus.FullPlayer,
            RegisteredAt = registeredAt,
            LastMatchAt = lastMatchAt,
            LastDecayAppliedAt = lastDecayAt,
            LastInactivityWarningAt = lastWarnAt,
            EloRating = rating,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task InsideGracePeriod_NoDecayNoWarning()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory, registeredAt: BaseDate, lastMatchAt: BaseDate.AddDays(-10));

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate.AddDays(5)); // 15 days since last match

        Assert.Equal(0, result.DecayEventsApplied);
        Assert.Equal(0, result.WarningsSent);

        using var verify = factory.CreateDbContext();
        var p = verify.CompetitionParticipants.Single(x => x.PlayerId == PlayerAId);
        Assert.Equal(1100, p.EloRating);
        Assert.Null(p.LastDecayAppliedAt);
        Assert.Null(p.LastInactivityWarningAt);
    }

    [Fact]
    public async Task ThreeDaysBeforeGraceEnd_SendsWarning_NoDecay()
    {
        var factory = new TestDbContextFactory();
        // 18 days idle => 3 days remaining in grace
        await SeedAsync(factory, registeredAt: BaseDate.AddDays(-30), lastMatchAt: BaseDate.AddDays(-18));

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate);

        Assert.Equal(0, result.DecayEventsApplied);
        Assert.Equal(1, result.WarningsSent);

        using var verify = factory.CreateDbContext();
        var p = verify.CompetitionParticipants.Single(x => x.PlayerId == PlayerAId);
        Assert.NotNull(p.LastInactivityWarningAt);
        Assert.Null(p.LastDecayAppliedAt);
    }

    [Fact]
    public async Task SecondSweepWithinGrace_DoesNotResendWarning()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory,
            registeredAt: BaseDate.AddDays(-30),
            lastMatchAt: BaseDate.AddDays(-18),
            lastWarnAt: BaseDate.AddHours(-2));

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate);

        Assert.Equal(0, result.WarningsSent);
    }

    [Fact]
    public async Task PastGrace_AppliesOneDecayStep()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory, registeredAt: BaseDate.AddDays(-40), lastMatchAt: BaseDate.AddDays(-25));

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate); // 25 days idle, 4 days post-grace

        Assert.Equal(1, result.DecayEventsApplied);

        using var verify = factory.CreateDbContext();
        var p = verify.CompetitionParticipants.Single(x => x.PlayerId == PlayerAId);
        Assert.Equal(1090, p.EloRating, 1);
        Assert.NotNull(p.LastDecayAppliedAt);

        var decay = verify.LadderInactivityDecays.Single();
        Assert.Equal(1100, decay.RatingBefore, 1);
        Assert.Equal(1090, decay.RatingAfter, 1);
        Assert.Equal(25, decay.DaysInactive);
    }

    [Fact]
    public async Task SameDayRerun_IsIdempotent()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory, registeredAt: BaseDate.AddDays(-40), lastMatchAt: BaseDate.AddDays(-25));

        using (var db = factory.CreateDbContext())
            await Run(db, BaseDate);
        using (var db = factory.CreateDbContext())
            await Run(db, BaseDate.AddHours(2)); // same day, slightly later

        using var verify = factory.CreateDbContext();
        var p = verify.CompetitionParticipants.Single(x => x.PlayerId == PlayerAId);
        // Only one decay step in total.
        Assert.Equal(1090, p.EloRating, 1);
        Assert.Single(verify.LadderInactivityDecays);
    }

    [Fact]
    public async Task SevenDaysAfterPriorDecay_AppliesAnother()
    {
        var factory = new TestDbContextFactory();
        var sweepDay1 = BaseDate;
        var sweepDay2 = BaseDate.AddDays(7);
        await SeedAsync(factory, registeredAt: BaseDate.AddDays(-40), lastMatchAt: BaseDate.AddDays(-25));

        using (var db = factory.CreateDbContext())
            await Run(db, sweepDay1);
        using (var db = factory.CreateDbContext())
            await Run(db, sweepDay2);

        using var verify = factory.CreateDbContext();
        var p = verify.CompetitionParticipants.Single(x => x.PlayerId == PlayerAId);
        Assert.Equal(1080, p.EloRating, 1);
        Assert.Equal(2, verify.LadderInactivityDecays.Count());
    }

    [Fact]
    public async Task DecayFlooredAtStartingRating()
    {
        var factory = new TestDbContextFactory();
        // Rating already 1003 — only 3 points of headroom above the 1000 floor.
        await SeedAsync(factory,
            registeredAt: BaseDate.AddDays(-60),
            lastMatchAt: BaseDate.AddDays(-30),
            rating: 1003);

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate);

        Assert.Equal(1, result.DecayEventsApplied);

        using var verify = factory.CreateDbContext();
        var p = verify.CompetitionParticipants.Single(x => x.PlayerId == PlayerAId);
        Assert.Equal(1000, p.EloRating, 1);

        // Another week later: at floor; no audit row but anchor still advances
        // so we don't recompute every day.
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate.AddDays(7));
        Assert.Equal(0, result.DecayEventsApplied);
        using var v2 = factory.CreateDbContext();
        Assert.Single(v2.LadderInactivityDecays);
    }

    [Fact]
    public async Task NonSinglesLadderFormat_NotTouched()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory,
            registeredAt: BaseDate.AddDays(-60),
            lastMatchAt: BaseDate.AddDays(-40),
            format: CompetitionFormat.Singles);

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate);

        Assert.Equal(0, result.DecayEventsApplied);
        Assert.Equal(0, result.WarningsSent);
        using var verify = factory.CreateDbContext();
        Assert.Equal(1100, verify.CompetitionParticipants.Single().EloRating);
    }

    [Fact]
    public async Task ArchivedCompetition_NotTouched()
    {
        var factory = new TestDbContextFactory();
        await SeedAsync(factory,
            registeredAt: BaseDate.AddDays(-60),
            lastMatchAt: BaseDate.AddDays(-40),
            isArchived: true);

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate);

        Assert.Equal(0, result.DecayEventsApplied);
    }

    [Fact]
    public async Task PlayerWithNoLastMatch_UsesRegisteredAt()
    {
        var factory = new TestDbContextFactory();
        // 25 days since registration, never played
        await SeedAsync(factory, registeredAt: BaseDate.AddDays(-25), lastMatchAt: null);

        DecayRunResult result;
        using (var db = factory.CreateDbContext())
            result = await Run(db, BaseDate);

        Assert.Equal(1, result.DecayEventsApplied);
    }

    private static Task<DecayRunResult> Run(DropShot.Data.MyDbContext db, DateTime now) =>
        LadderInactivityService.RunSweepAsync(db, now, email: null);
}
