using DropShot.E2E.Helpers;

namespace DropShot.E2E.PublicPages;

[TestFixture]
public class MatchPageTests : DropShotPageTest
{
    [Test]
    public async Task MatchPage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/match");
    }

    [Test]
    public async Task ScoreboardPage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/score");
    }

    [Test]
    public async Task CounterPage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/counter", "Counter");
    }
}
