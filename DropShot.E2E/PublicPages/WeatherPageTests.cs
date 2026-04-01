using DropShot.E2E.Helpers;

namespace DropShot.E2E.PublicPages;

[TestFixture]
public class WeatherPageTests : DropShotPageTest
{
    [Test]
    public async Task WeatherPage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/weather", "Weather");
    }

    [Test]
    public async Task WeatherPage_Shows_Forecast_Table()
    {
        await Page.GotoAsync($"{BaseUrl}/weather");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for data to load (has a Task.Delay in the component)
        await Page.WaitForSelectorAsync("table", new() { Timeout = 5000 });

        var table = Page.Locator("table");
        await Expect(table).ToBeVisibleAsync();
    }
}
