using Bunit;
using DropShot.Components.Pages;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class ClubsTests
{
    [Fact]
    public async Task Clubs_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<Clubs>();

        Assert.Contains("Clubs", cut.Markup);
    }

    [Fact]
    public async Task Clubs_Shows_Seeded_Data()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);

        using (var db = ctx.SeedDatabase())
        {
            db.Clubs.Add(new Club { ClubId = 1, Name = "Wimbledon Tennis Club" });
            db.SaveChanges();
        }

        var cut = ctx.Render<Clubs>();

        Assert.Contains("Wimbledon Tennis Club", cut.Markup);
    }
}
