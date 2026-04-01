using Bunit;
using DropShot.Components.Pages;
using DropShot.Data;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DropShot.Tests.Pages;

public class HomeTests
{
    [Fact]
    public async Task Home_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<Home>();

        cut.Markup.Contains("Welcome");
    }

    [Fact]
    public async Task Home_Authenticated_Renders_Carousel_And_Welcome()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<Home>();

        Assert.Contains("Welcome", cut.Markup);
        Assert.Contains("mud-carousel", cut.Markup);
    }

    [Fact]
    public async Task Home_With_Fixtures_Shows_Upcoming_Matches()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-1");

        using (var db = ctx.SeedDatabase())
        {
            var player = new Player { PlayerId = 1, DisplayName = "Test Player", UserId = "user-1" };
            var opponent = new Player { PlayerId = 2, DisplayName = "Opponent" };
            db.Players.AddRange(player, opponent);

            var comp = new Competition { CompetitionID = 1, CompetitionName = "Test Comp" };
            db.Competition.Add(comp);

            var stage = new CompetitionStage { CompetitionStageId = 1, Name = "Group", CompetitionId = 1 };
            db.CompetitionStages.Add(stage);

            db.CompetitionFixtures.Add(new CompetitionFixture
            {
                CompetitionFixtureId = 1,
                CompetitionId = 1,
                CompetitionStageId = 1,
                Player1Id = 1,
                Player2Id = 2,
                Status = FixtureStatus.Scheduled,
                ScheduledAt = DateTime.UtcNow.AddDays(1)
            });
            db.SaveChanges();
        }

        var cut = ctx.Render<Home>();

        Assert.Contains("Your Upcoming Matches", cut.Markup);
    }
}
