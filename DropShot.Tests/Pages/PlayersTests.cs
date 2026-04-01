using Bunit;
using DropShot.Components.Pages;
using DropShot.Data;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class PlayersTests
{
    [Fact]
    public async Task Players_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<Players>();

        Assert.Contains("Players", cut.Markup);
    }

    [Fact]
    public async Task Players_Shows_Seeded_Data()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);

        using (var db = ctx.SeedDatabase())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "Alice Smith" });
            db.Players.Add(new Player { PlayerId = 2, DisplayName = "Bob Jones" });
            db.SaveChanges();
        }

        var cut = ctx.Render<Players>();

        Assert.Contains("Alice Smith", cut.Markup);
        Assert.Contains("Bob Jones", cut.Markup);
    }
}
