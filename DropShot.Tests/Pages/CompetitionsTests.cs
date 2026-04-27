using Bunit;
using DropShot.Components.Pages;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class CompetitionsTests
{
    [Fact]
    public async Task Competitions_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, roles: ["Admin"]);
        var cut = ctx.Render<Competitions>();

        Assert.Contains("Search competitions", cut.Markup);
    }

    [Fact]
    public async Task Competitions_Shows_Seeded_Data()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, roles: ["Admin"]);

        using (var db = ctx.SeedDatabase())
        {
            db.Competition.Add(new Competition { CompetitionID = 1, CompetitionName = "Summer Open 2025" });
            db.SaveChanges();
        }

        var cut = ctx.Render<Competitions>();

        Assert.Contains("Summer Open 2025", cut.Markup);
    }
}
