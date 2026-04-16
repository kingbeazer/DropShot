using System.Security.Claims;
using DropShot.Data;
using DropShot.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace DropShot.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="ClubAuthorizationService.CanCreateUserCompetition"/>.
/// Kept isolated from the bUnit context because the service is substituted there.
/// </summary>
public class ClubAuthorizationServiceTests
{
    private static (ClubAuthorizationService svc, UserManager<ApplicationUser> userManager) BuildService(string userId)
    {
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null!, null!, null!, null!, null!, null!, null!, null!);

        // The service reads the user id via UserManager.GetUserId(ClaimsPrincipal).
        userManager.GetUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        var dbFactory = new TestDbContextFactory();
        return (new ClubAuthorizationService(userManager, dbFactory), userManager);
    }

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-id-1")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void CanCreateUserCompetition_Subscribed_UserRole_ReturnsTrue()
    {
        var (svc, _) = BuildService("user-id-1");
        var principal = PrincipalWithRoles("User");
        var user = new ApplicationUser { Id = "user-id-1", IsSubscribed = true };

        Assert.True(svc.CanCreateUserCompetition(principal, user));
    }

    [Fact]
    public void CanCreateUserCompetition_NotSubscribed_UserRole_ReturnsFalse()
    {
        var (svc, _) = BuildService("user-id-1");
        var principal = PrincipalWithRoles("User");
        var user = new ApplicationUser { Id = "user-id-1", IsSubscribed = false };

        Assert.False(svc.CanCreateUserCompetition(principal, user));
    }

    [Fact]
    public void CanCreateUserCompetition_ClubAdminRole_Subscribed_ReturnsFalse()
    {
        // Per product decision: a ClubAdmin must switch to "User" mode to create
        // a user competition. Being subscribed while acting as ClubAdmin is not enough.
        var (svc, _) = BuildService("user-id-1");
        var principal = PrincipalWithRoles("ClubAdmin");
        var user = new ApplicationUser { Id = "user-id-1", IsSubscribed = true };

        Assert.False(svc.CanCreateUserCompetition(principal, user));
    }

    [Fact]
    public void CanCreateUserCompetition_AdminRole_BypassesSubscription()
    {
        var (svc, _) = BuildService("user-id-1");
        var principal = PrincipalWithRoles("Admin");
        var user = new ApplicationUser { Id = "user-id-1", IsSubscribed = false };

        Assert.True(svc.CanCreateUserCompetition(principal, user));
    }

    [Fact]
    public void CanCreateUserCompetition_SuperAdminRole_BypassesSubscription()
    {
        var (svc, _) = BuildService("user-id-1");
        var principal = PrincipalWithRoles("SuperAdmin");
        var user = new ApplicationUser { Id = "user-id-1", IsSubscribed = false };

        Assert.True(svc.CanCreateUserCompetition(principal, user));
    }

    [Fact]
    public void CanCreateUserCompetition_NullAppUser_ReturnsFalse()
    {
        var (svc, _) = BuildService("user-id-1");
        var principal = PrincipalWithRoles("User");

        Assert.False(svc.CanCreateUserCompetition(principal, null));
    }
}
