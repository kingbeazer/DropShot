using Bunit;
using DropShot.Components.Pages;
using DropShot.Tests.Helpers;
using Xunit;

namespace DropShot.Tests.Pages;

public class VerifyResultTests
{
    [Fact]
    public async Task VerifyResult_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(authenticated: false);
        var cut = ctx.Render<VerifyResult>(p => p.Add(x => x.Token, "test-token-123"));

        Assert.NotEmpty(cut.Markup);
    }
}
