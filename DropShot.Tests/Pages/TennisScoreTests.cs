using Bunit;
using DropShot.Components.Pages;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DropShot.Tests.Pages;

public class TennisScoreTests
{
    [Fact]
    public async Task TennisScore_Renders_Without_Exception_For_Subscriber()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, subscribed: true);

        using (var db = ctx.SeedDatabase())
        {
            db.SavedMatch.Add(new SavedMatch { SavedMatchId = 1 });
            db.SaveChanges();
        }

        var cut = ctx.Render<TennisScore>(p => p.Add(x => x.MatchId, 1));

        Assert.NotEmpty(cut.Markup);
    }

    [Fact]
    public async Task TennisScore_Redirects_Home_For_Anonymous_User()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);

        using (var db = ctx.SeedDatabase())
        {
            db.SavedMatch.Add(new SavedMatch { SavedMatchId = 1 });
            db.SaveChanges();
        }

        ctx.Render<TennisScore>(p => p.Add(x => x.MatchId, 1));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/", nav.Uri);
    }

    [Fact]
    public async Task TennisScore_Redirects_Home_For_Unsubscribed_User()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, subscribed: false);

        using (var db = ctx.SeedDatabase())
        {
            db.SavedMatch.Add(new SavedMatch { SavedMatchId = 1 });
            db.SaveChanges();
        }

        ctx.Render<TennisScore>(p => p.Add(x => x.MatchId, 1));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/", nav.Uri);
    }
}
