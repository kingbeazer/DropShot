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
    private async Task<(ApplicationUser? user, IList<string> roles)> GetUserAndRolesAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return (null, Array.Empty<string>());
        var roles = await userManager.GetRolesAsync(user);
        return (user, roles);
    }

    /// <summary>Returns true if the user holds the SuperAdmin role.</summary>
    public async Task<bool> IsSuperAdminAsync(ClaimsPrincipal user)
    {
        var (appUser, roles) = await GetUserAndRolesAsync(user);
        return appUser is not null && roles.Contains("SuperAdmin");
    }

    /// <summary>Returns true if the user holds the Admin or SuperAdmin role.</summary>
    public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
    {
        var (appUser, roles) = await GetUserAndRolesAsync(user);
        return appUser is not null && (roles.Contains("Admin") || roles.Contains("SuperAdmin"));
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
        var (appUser, roles) = await GetUserAndRolesAsync(user);
        if (appUser is null) return false;
        if (roles.Contains("Admin") || roles.Contains("SuperAdmin")) return true;

        await using var db = dbFactory.CreateDbContext();
        return await db.ClubAdministrators
            .AnyAsync(ca => ca.UserId == appUser.Id && ca.ClubId == clubId);
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
        var (appUser, roles) = await GetUserAndRolesAsync(user);
        if (appUser is null) return false;
        if (roles.Contains("Admin") || roles.Contains("SuperAdmin")) return true;

        if (competitionId.HasValue)
        {
            await using var db = dbFactory.CreateDbContext();
            if (await db.CompetitionAdmins.AnyAsync(ca =>
                    ca.CompetitionId == competitionId.Value && ca.UserId == appUser.Id))
                return true;
        }

        if (hostClubId is null) return false;

        await using var db2 = dbFactory.CreateDbContext();
        return await db2.ClubAdministrators
            .AnyAsync(ca => ca.UserId == appUser.Id && ca.ClubId == hostClubId.Value);
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
