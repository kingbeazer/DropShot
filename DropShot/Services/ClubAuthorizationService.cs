using System.Security.Claims;
using DropShot.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Provides fine-grained authorization checks for club and competition editing.
/// Inject into Blazor pages and pass the current ClaimsPrincipal.
/// </summary>
public class ClubAuthorizationService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<MyDbContext> dbFactory)
{
    /// <summary>Returns true if the user holds the global Admin role.</summary>
    public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
    {
        var appUser = await userManager.GetUserAsync(user);
        if (appUser is null) return false;
        return await userManager.IsInRoleAsync(appUser, "Admin");
    }

    /// <summary>Returns the list of ClubIds the user is an administrator of.</summary>
    public async Task<List<int>> GetAdminClubIdsAsync(ClaimsPrincipal user)
    {
        var userId = userManager.GetUserId(user);
        if (userId is null) return [];

        await using var db = dbFactory.CreateDbContext();
        return await db.ClubAdministrators
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.ClubId)
            .ToListAsync();
    }

    /// <summary>Returns true if the user can edit the given club (Admin or assigned ClubAdmin).</summary>
    public async Task<bool> CanEditClubAsync(ClaimsPrincipal user, int clubId)
    {
        if (await IsAdminAsync(user)) return true;

        var userId = userManager.GetUserId(user);
        if (userId is null) return false;

        await using var db = dbFactory.CreateDbContext();
        return await db.ClubAdministrators
            .AnyAsync(ca => ca.UserId == userId && ca.ClubId == clubId);
    }

    /// <summary>
    /// Returns true if the user can edit the given competition.
    /// Admin can edit any competition. ClubAdmin can only edit competitions
    /// that have a host club they administer. Competitions with no host club
    /// are Admin-only.
    /// </summary>
    public async Task<bool> CanEditCompetitionAsync(ClaimsPrincipal user, int? hostClubId)
    {
        if (await IsAdminAsync(user)) return true;
        if (hostClubId is null) return false;
        return await CanEditClubAsync(user, hostClubId.Value);
    }
}
