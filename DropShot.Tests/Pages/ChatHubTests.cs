using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class ChatHubTests
{
    [Fact]
    public async Task ChatHub_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);

        // ChatHub uses SignalR which won't connect in tests,
        // but the initial render should succeed
        var cut = ctx.Render<ChatHub>();

        Assert.NotEmpty(cut.Markup);
    }
}
