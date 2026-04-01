using DropShot.E2E.Helpers;

namespace DropShot.E2E.PublicPages;

[TestFixture]
public class HomePageTests : DropShotPageTest
{
    [Test]
    public async Task HomePage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/", "Welcome");
    }

    [Test]
    public async Task HomePage_Shows_Carousel()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var carousel = Page.Locator(".mud-carousel");
        await Expect(carousel).ToBeVisibleAsync();
    }
}
