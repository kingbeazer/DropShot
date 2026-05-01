using Bunit;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class ViewCompetitionTests
{
    [Fact]
    public async Task ViewCompetition_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, roles: ["Admin"]);

        using (var db = ctx.SeedDatabase())
        {
            db.Competition.Add(new Competition
            {
                CompetitionID = 1,
                CompetitionName = "Spring League",
                IsStarted = true,
            });
            db.SaveChanges();
        }

        var cut = ctx.Render<DropShot.UI.Components.Pages.ViewCompetition>(
            p => p.Add(x => x.Id, 1));

        // Asserting only that the component renders without exception. Visibility
        // gating is owned by ClubAuthorizationService (substituted; methods are
        // non-virtual so NSubstitute can't intercept) — the deep "I see fixtures"
        // assertion belongs in an integration test, not bUnit.
        Assert.NotEmpty(cut.Markup);
    }

    [Fact]
    public async Task ViewCompetition_Missing_Id_Renders_Gracefully()
    {
        await using var ctx = new DropShotTestContext(authenticated: true);
        var cut = ctx.Render<DropShot.UI.Components.Pages.ViewCompetition>(
            p => p.Add(x => x.Id, 999));

        Assert.NotEmpty(cut.Markup);
    }
}
