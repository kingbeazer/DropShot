using System.Security.Claims;
using DropShot.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Provides fine-grained authorization checks for club and competition editing.
/// Inject into Blazor pages and pass the current ClaimsPrincipal.
///
/// Role checks read from the ClaimsPrincipal's Role claims, which are filtered
/// by ActiveRoleAuthenticationStateProvider to contain only the active role.
/// This means all checks automatically respect the user's active role selection.
/// </summary>
public class ClubAuthorizationService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<MyDbContext> dbFactory)
{
    private (string? userId, IList<string> roles) GetUserAndRoles(ClaimsPrincipal principal)
    {
        var userId = userManager.GetUserId(principal);
        if (userId is null) return (null, Array.Empty<string>());

        // Read roles from claims (filtered to active role by the auth state provider)
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return (userId, roles);
    }

    /// <summary>Returns true if the user's active role is SuperAdmin.</summary>
    public bool IsSuperAdmin(ClaimsPrincipal user)
    {
        var (userId, roles) = GetUserAndRoles(user);
        return userId is not null && roles.Contains("SuperAdmin");
    }

    /// <summary>Returns true if the user's active role is Admin or SuperAdmin.</summary>
    public bool IsAdmin(ClaimsPrincipal user)
    {
        var (userId, roles) = GetUserAndRoles(user);
        return userId is not null && (roles.Contains("Admin") || roles.Contains("SuperAdmin"));
    }

    // Keep async overloads for backward compatibility with existing callers
    public Task<bool> IsSuperAdminAsync(ClaimsPrincipal user) => Task.FromResult(IsSuperAdmin(user));
    public Task<bool> IsAdminAsync(ClaimsPrincipal user) => Task.FromResult(IsAdmin(user));

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

    /// <summary>Returns true if the user can edit the given club (Admin active role or assigned ClubAdmin).</summary>
    public async Task<bool> CanEditClubAsync(ClaimsPrincipal user, int clubId)
    {
        var (userId, roles) = GetUserAndRoles(user);
        if (userId is null) return false;
        if (roles.Contains("Admin") || roles.Contains("SuperAdmin")) return true;

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
        var (userId, roles) = GetUserAndRoles(user);
        if (userId is null) return false;
        if (roles.Contains("Admin") || roles.Contains("SuperAdmin")) return true;

        if (competitionId.HasValue)
        {
            await using var db = dbFactory.CreateDbContext();
            if (await db.CompetitionAdmins.AnyAsync(ca =>
                    ca.CompetitionId == competitionId.Value && ca.UserId == userId))
                return true;
        }

        if (hostClubId is null) return false;

        await using var db2 = dbFactory.CreateDbContext();
        return await db2.ClubAdministrators
            .AnyAsync(ca => ca.UserId == userId && ca.ClubId == hostClubId.Value);
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
