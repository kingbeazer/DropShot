using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class LeagueTableTests
{
    [Fact]
    public async Task LeagueTable_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<LeagueTable>();

        Assert.NotEmpty(cut.Markup);
    }
}
