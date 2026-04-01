using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class ScoreboardTests
{
    [Fact]
    public async Task Scoreboard_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);

        // Scoreboard uses SignalR which won't connect in tests,
        // but the initial render should succeed
        var cut = ctx.Render<Scoreboard>();

        Assert.NotEmpty(cut.Markup);
    }
}
