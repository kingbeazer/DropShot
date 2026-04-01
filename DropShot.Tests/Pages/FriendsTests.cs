using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class FriendsTests
{
    [Fact]
    public async Task Friends_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<Friends>();

        Assert.Contains("Friends", cut.Markup);
    }
}
