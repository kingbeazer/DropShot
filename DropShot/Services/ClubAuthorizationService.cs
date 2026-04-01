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
    /// <summary>Returns true if the user holds the SuperAdmin role.</summary>
    public async Task<bool> IsSuperAdminAsync(ClaimsPrincipal user)
    {
        var appUser = await userManager.GetUserAsync(user);
        if (appUser is null) return false;
        return await userManager.IsInRoleAsync(appUser, "SuperAdmin");
    }

    /// <summary>Returns true if the user holds the Admin or SuperAdmin role.</summary>
    public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
    {
        var appUser = await userManager.GetUserAsync(user);
        if (appUser is null) return false;
        return await userManager.IsInRoleAsync(appUser, "Admin")
            || await userManager.IsInRoleAsync(appUser, "SuperAdmin");
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
    /// that have a host club they administer. CompetitionAdmins can edit
    /// their specific competitions. Competitions with no host club
    /// are Admin-only (unless they're a CompetitionAdmin for it).
    /// </summary>
    public async Task<bool> CanEditCompetitionAsync(
        ClaimsPrincipal user, int? hostClubId, int? competitionId = null)
    {
        if (await IsAdminAsync(user)) return true;

        var userId = userManager.GetUserId(user);
        if (userId != null && competitionId.HasValue)
        {
            await using var db = dbFactory.CreateDbContext();
            if (await db.CompetitionAdmins.AnyAsync(ca =>
                    ca.CompetitionId == competitionId.Value && ca.UserId == userId))
                return true;
        }

        if (hostClubId is null) return false;
        return await CanEditClubAsync(user, hostClubId.Value);
    }

    /// <summary>Returns the set of CompetitionIds the user is a per-competition admin of.</summary>
    public async Task<HashSet<int>> GetEditableCompetitionIdsAsync(ClaimsPrincipal user)
    {
        var userId = userManager.GetUserId(user);
        if (userId is null) return [];

        await using var db = dbFactory.CreateDbContext();
        return (await db.CompetitionAdmins
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.CompetitionId)
            .ToListAsync()).ToHashSet();
    }
}
