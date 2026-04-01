using Bunit;
using DropShot.Components.Pages;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class RulesSetsTests
{
    [Fact]
    public async Task RulesSets_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<RulesSets>();

        Assert.Contains("Rules", cut.Markup);
    }

    [Fact]
    public async Task RulesSets_Shows_Seeded_Data()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);

        using (var db = ctx.SeedDatabase())
        {
            db.RulesSets.Add(new RulesSet { RulesSetId = 1, Name = "Standard Rules" });
            db.SaveChanges();
        }

        var cut = ctx.Render<RulesSets>();

        Assert.Contains("Standard Rules", cut.Markup);
    }
}
