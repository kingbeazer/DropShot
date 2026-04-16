using System.Security.Claims;
using DropShot.Data;
using DropShot.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Snapshot of the caller's identity + Player graph used to evaluate which
/// competitions they can see/enter. Built once per request by
/// <see cref="ClubAuthorizationService.GetVisibilityContextAsync"/> and reused
/// by both the list filter and single-row checks.
/// </summary>
public sealed record VisibilityContext(
    bool IsAdmin,
    string? UserId,
    int? PlayerId,
    int? Age,
    PlayerSex? Sex,
    HashSet<int> ClubIdsMember,
    HashSet<int> FriendPlayerIds,
    HashSet<int> OwnCompetitionIds);

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
        var (userId, roles) = GetUserAndRoles(user);
        if (userId is null) return [];

        // If the active role is plain User, they have no club admin privileges
        if (roles.Count == 1 && roles[0] == "User") return [];

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

        // Plain User role has no club admin privileges
        if (roles.Count == 1 && roles[0] == "User") return false;

        await using var db = dbFactory.CreateDbContext();
        return await db.ClubAdministrators
            .AnyAsync(ca => ca.UserId == userId && ca.ClubId == clubId);
    }

    /// <summary>
    /// Returns true if the user can edit the given competition.
    /// Admin can edit any competition. ClubAdmin can only edit competitions
    /// that have a host club they administer. CompetitionAdmins can edit
    /// their specific competitions. For user competitions (no host club), the
    /// creator can edit their own.
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

            // The creator of a user competition can edit their own.
            var creatorId = await db.Competition
                .Where(c => c.CompetitionID == competitionId.Value)
                .Select(c => c.CreatorUserId)
                .FirstOrDefaultAsync();
            if (creatorId == userId) return true;
        }

        // Plain User role has no club admin privileges
        if (roles.Count == 1 && roles[0] == "User") return false;

        if (hostClubId is null) return false;

        await using var db2 = dbFactory.CreateDbContext();
        return await db2.ClubAdministrators
            .AnyAsync(ca => ca.UserId == userId && ca.ClubId == hostClubId.Value);
    }

    /// <summary>Returns the set of CompetitionIds the user is a per-competition admin of.</summary>
    public async Task<HashSet<int>> GetEditableCompetitionIdsAsync(ClaimsPrincipal user)
    {
        var (userId, roles) = GetUserAndRoles(user);
        if (userId is null) return [];

        // If the active role is plain User, they have no competition admin privileges
        if (roles.Count == 1 && roles[0] == "User") return [];

        await using var db = dbFactory.CreateDbContext();
        return (await db.CompetitionAdmins
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.CompetitionId)
            .ToListAsync()).ToHashSet();
    }

    // ── New premium / visibility gates ─────────────────────────────────────────

    /// <summary>
    /// Returns true if the caller can create a "user competition" (no host club).
    /// Requires an active "User" role AND <see cref="ApplicationUser.IsSubscribed"/>=true.
    /// Admin and SuperAdmin are also allowed as an escape hatch.
    /// </summary>
    public bool CanCreateUserCompetition(ClaimsPrincipal user, ApplicationUser? appUser)
    {
        var (userId, roles) = GetUserAndRoles(user);
        if (userId is null || appUser is null) return false;

        if (roles.Contains("SuperAdmin") || roles.Contains("Admin")) return true;

        // Must be acting as a plain User (not ClubAdmin mode) AND be subscribed.
        var isUserRoleActive = roles.Count == 1 && roles[0] == "User";
        return isUserRoleActive && appUser.IsSubscribed;
    }

    /// <summary>
    /// Returns true if the caller can create a competition hosted by the given club.
    /// Currently delegates to <see cref="CanEditClubAsync"/>.
    /// </summary>
    public Task<bool> CanCreateClubCompetitionAsync(ClaimsPrincipal user, int clubId)
        => CanEditClubAsync(user, clubId);

    /// <summary>
    /// Builds a one-shot visibility snapshot for the caller. Cheap enough to be
    /// called per request; reuses a single DbContext.
    /// </summary>
    public async Task<VisibilityContext> GetVisibilityContextAsync(ClaimsPrincipal user)
    {
        var (userId, roles) = GetUserAndRoles(user);
        var isAdmin = userId is not null && (roles.Contains("Admin") || roles.Contains("SuperAdmin"));

        if (userId is null)
            return new VisibilityContext(false, null, null, null, null, new(), new(), new());

        await using var db = dbFactory.CreateDbContext();

        // Caller's Player (if any).
        var player = await db.Players
            .Where(p => p.UserId == userId && !p.IsLight)
            .Select(p => new { p.PlayerId, p.DateOfBirth, p.Sex })
            .FirstOrDefaultAsync();

        int? playerId = player?.PlayerId;
        PlayerSex? sex = player?.Sex;
        int? age = null;
        if (player?.DateOfBirth is DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            int a = today.Year - dob.Year;
            if (dob > today.AddYears(-a)) a--;
            age = a;
        }

        HashSet<int> clubIds = new();
        HashSet<int> friendIds = new();

        if (playerId is int pid)
        {
            clubIds = (await db.ClubPlayers
                .Where(cp => cp.PlayerId == pid && cp.IsActive)
                .Select(cp => cp.ClubId)
                .ToListAsync()).ToHashSet();

            friendIds = (await db.PlayerFriends
                .Where(pf => pf.Status == FriendStatus.Accepted
                             && (pf.PlayerId == pid || pf.FriendPlayerId == pid))
                .Select(pf => pf.PlayerId == pid ? pf.FriendPlayerId : pf.PlayerId)
                .ToListAsync()).ToHashSet();
        }

        var ownCompIds = (await db.CompetitionAdmins
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.CompetitionId)
            .ToListAsync()).ToHashSet();

        return new VisibilityContext(isAdmin, userId, playerId, age, sex, clubIds, friendIds, ownCompIds);
    }

    /// <summary>
    /// Applies the hard visibility + age/sex filter to a queryable of
    /// <see cref="Competition"/>. The <paramref name="db"/> context is required
    /// because the user-competition branch needs a sub-query over
    /// <c>Players.Friends</c> — passing the same context EF was built against
    /// keeps this a single SQL statement.
    /// </summary>
    public IQueryable<Competition> ApplyVisibilityFilter(
        IQueryable<Competition> q, MyDbContext db, VisibilityContext ctx, DateTime? now = null)
    {
        if (ctx.IsAdmin) return q;

        var playerId = ctx.PlayerId;
        var userId = ctx.UserId;
        var clubIds = ctx.ClubIdsMember;
        var friendIds = ctx.FriendPlayerIds;
        var ownComps = ctx.OwnCompetitionIds;
        var age = ctx.Age;
        var sex = ctx.Sex;

        return q.Where(c =>
            // Access rule
            ownComps.Contains(c.CompetitionID) ||
            (c.HostClubId != null && clubIds.Contains(c.HostClubId.Value)
                && (!c.IsRestricted || c.AllowedPlayers.Any(ap => playerId != null && ap.PlayerId == playerId)))
            ||
            (c.HostClubId == null && c.CreatorUserId != null
                && (c.CreatorUserId == userId
                    || (playerId != null && db.Players.Any(p =>
                        p.UserId == c.CreatorUserId && friendIds.Contains(p.PlayerId))))
                && (!c.IsRestricted || c.AllowedPlayers.Any(ap => playerId != null && ap.PlayerId == playerId)))
        )
        .Where(c =>
            // Age/sex eligibility: treat as hard filter per product decision.
            (c.EligibleSex == null || c.EligibleSex == sex) &&
            (c.MinAge == null || (age != null && age >= c.MinAge)) &&
            (c.MaxAge == null || (age != null && age <= c.MaxAge))
        );
    }

    /// <summary>
    /// Single-row visibility check. Uses <see cref="ApplyVisibilityFilter"/> to
    /// avoid drift between list and detail.
    /// </summary>
    public async Task<bool> CanViewCompetitionAsync(ClaimsPrincipal user, int competitionId)
    {
        var ctx = await GetVisibilityContextAsync(user);
        await using var db = dbFactory.CreateDbContext();
        var q = db.Competition.Where(c => c.CompetitionID == competitionId);
        return await ApplyVisibilityFilter(q, db, ctx).AnyAsync();
    }
}
