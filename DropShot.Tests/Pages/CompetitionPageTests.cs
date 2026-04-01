using Bunit;
using DropShot.Components.Pages;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class CompetitionPageTests
{
    [Fact]
    public async Task CompetitionPage_New_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<CompetitionPage>(p => p.Add(x => x.Id, 0));

        // New competition page should render
        Assert.NotEmpty(cut.Markup);
    }

    [Fact]
    public async Task CompetitionPage_Existing_Renders_With_Data()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);

        using (var db = ctx.SeedDatabase())
        {
            db.Competition.Add(new Competition { CompetitionID = 1, CompetitionName = "Test Cup" });
            db.SaveChanges();
        }

        var cut = ctx.Render<CompetitionPage>(p => p.Add(x => x.Id, 1));

        Assert.Contains("Test Cup", cut.Markup);
    }
}
