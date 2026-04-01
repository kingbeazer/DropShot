using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class WeatherTests
{
    [Fact]
    public async Task Weather_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<Weather>();

        Assert.Contains("Weather", cut.Markup);
    }

    [Fact]
    public async Task Weather_Shows_Table_After_Loading()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<Weather>();

        // Wait for async data load
        await Task.Delay(600);
        cut.Render();

        Assert.Contains("table", cut.Markup);
        Assert.Contains("Temp. (C)", cut.Markup);
    }
}
