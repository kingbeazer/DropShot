using Bunit;
using DropShot.Tests.Helpers;
using MudBlazor;
using Xunit;

namespace DropShot.Tests.Pages;

public class ScoreboardTests
{
    [Fact]
    public async Task Scoreboard_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);

        // The moved RCL Scoreboard expects MudProviders to be supplied by the host
        // (web/MAUI shims do this); render them inline here so the popover provider
        // exists for MudSelect inside the page. SignalR won't connect in tests.
        var cut = ctx.Render(b =>
        {
            b.OpenComponent<MudPopoverProvider>(0);
            b.CloseComponent();
            b.OpenComponent<MudDialogProvider>(1);
            b.CloseComponent();
            b.OpenComponent<MudSnackbarProvider>(2);
            b.CloseComponent();
            b.OpenComponent<DropShot.UI.Components.Pages.Scoreboard>(3);
            b.CloseComponent();
        });

        Assert.NotEmpty(cut.Markup);
    }
}
