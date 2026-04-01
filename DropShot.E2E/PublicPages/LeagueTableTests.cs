using DropShot.E2E.Helpers;

namespace DropShot.E2E.PublicPages;

[TestFixture]
public class LeagueTableTests : DropShotPageTest
{
    [Test]
    public async Task LeagueTablePage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/league");
    }
}
