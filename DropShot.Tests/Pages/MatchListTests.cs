using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DropShot.Tests.Pages;

public class MatchListTests
{
    [Fact]
    public async Task Match_Renders_Without_Exception_For_Subscriber()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, subscribed: true);
        var cut = ctx.Render<Match>();

        Assert.NotEmpty(cut.Markup);
    }

    [Fact]
    public async Task Match_Redirects_Home_For_Anonymous_User()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        ctx.Render<Match>();

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/", nav.Uri);
    }

    [Fact]
    public async Task Match_Redirects_Home_For_Unsubscribed_User()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, subscribed: false);
        ctx.Render<Match>();

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/", nav.Uri);
    }
}
