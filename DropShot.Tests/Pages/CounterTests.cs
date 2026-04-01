using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class CounterTests
{
    [Fact]
    public async Task Counter_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<Counter>();

        Assert.Contains("Counter", cut.Markup);
        Assert.Contains("Current count: 0", cut.Markup);
    }

    [Fact]
    public async Task Counter_Increments_On_Click()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<Counter>();

        cut.Find("button").Click();

        Assert.Contains("Current count: 1", cut.Markup);
    }
}
