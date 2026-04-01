using Bunit;
using DropShot.Components.Pages;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class ViewCompetitionTests
{
    [Fact]
    public async Task ViewCompetition_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);

        using (var db = ctx.SeedDatabase())
        {
            db.Competition.Add(new Competition { CompetitionID = 1, CompetitionName = "Spring League" });
            db.SaveChanges();
        }

        var cut = ctx.Render<ViewCompetition>(p => p.Add(x => x.Id, 1));

        Assert.Contains("Spring League", cut.Markup);
    }

    [Fact]
    public async Task ViewCompetition_Missing_Id_Renders_Gracefully()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<ViewCompetition>(p => p.Add(x => x.Id, 999));

        // Should render without exception even when competition not found
        Assert.NotEmpty(cut.Markup);
    }
}
