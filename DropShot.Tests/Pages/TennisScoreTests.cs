using Bunit;
using DropShot.Components;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class TennisScoreTests
{
    [Fact]
    public async Task TennisScore_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);

        using (var db = ctx.SeedDatabase())
        {
            db.SavedMatch.Add(new SavedMatch { SavedMatchId = 1 });
            db.SaveChanges();
        }

        var cut = ctx.Render<TennisScore>(p => p.Add(x => x.MatchId, 1));

        Assert.NotEmpty(cut.Markup);
    }
}
