using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class AuthTests
{
    [Fact]
    public async Task Auth_Authenticated_Shows_User_Info()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userName: "player@test.com");
        var cut = ctx.Render<Auth>();

        Assert.Contains("You are authenticated", cut.Markup);
        Assert.Contains("player@test.com", cut.Markup);
    }
}
