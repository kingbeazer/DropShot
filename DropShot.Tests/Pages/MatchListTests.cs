using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class MatchListTests
{
    [Fact]
    public async Task Match_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<Match>();

        Assert.NotEmpty(cut.Markup);
    }
}
