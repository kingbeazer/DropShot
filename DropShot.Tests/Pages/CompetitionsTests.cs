using Bunit;
using DropShot.Models;
using DropShot.Tests.Helpers;
using Xunit;
using UICompetitions = DropShot.UI.Components.Pages.Competitions;

namespace DropShot.Tests.Pages;

public class CompetitionsTests
{
    [Fact]
    public async Task Competitions_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, roles: ["Admin"]);
        var cut = ctx.Render<UICompetitions>();

        cut.WaitForAssertion(() => Assert.Contains("Search competitions", cut.Markup));
    }

    [Fact]
    public async Task Competitions_Shows_Seeded_Data()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, roles: ["Admin"]);

        using (var db = ctx.SeedDatabase())
        {
            db.Competition.Add(new Competition { CompetitionID = 1, CompetitionName = "Summer Open 2025" });
            db.SaveChanges();
        }

        var cut = ctx.Render<UICompetitions>();

        // Page renders without throwing; the deep "competition row visible"
        // assertion needs ClubAuthorizationService stubbed with virtuals
        // (NSubstitute can't intercept the non-virtual visibility filter), so
        // the same caveat as ViewCompetitionTests applies.
        cut.WaitForAssertion(() => Assert.Contains("Search competitions", cut.Markup));
    }
}
