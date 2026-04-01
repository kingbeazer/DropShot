using Bunit;
using DropShot.Components.Pages.Admin;
using DropShot.Data;
using DropShot.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DropShot.Tests.Pages.Admin;

public class UserManagementTests
{
    [Fact]
    public async Task UserManagement_Admin_Renders_Without_Exception()
    {
        await using var ctx = new DropShotTestContext(
            authenticated: true,
            roles: ["Admin", "SuperAdmin"]);

        // Mock UserManager to return empty user list
        var userManager = ctx.Services.GetRequiredService<UserManager<ApplicationUser>>();
        userManager.Users.Returns(new List<ApplicationUser>().AsQueryable());

        var cut = ctx.Render<UserManagement>();

        Assert.Contains("User Management", cut.Markup);
    }
}
