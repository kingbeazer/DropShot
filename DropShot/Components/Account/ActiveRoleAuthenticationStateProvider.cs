using System.Security.Claims;
using DropShot.Data;
using DropShot.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DropShot.Components.Account;

/// <summary>
/// Wraps the Identity revalidating auth state provider to filter role claims
/// based on the user's active role (stored in an HttpOnly cookie).
/// This ensures [Authorize(Roles=...)] and AuthorizeView automatically
/// respect the active role selection.
/// </summary>
internal sealed class ActiveRoleAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ActiveRoleService activeRoleService)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await ValidateSecurityStampAsync(userManager, authenticationState.User);
    }

    private async Task<bool> ValidateSecurityStampAsync(
        UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return false;
        if (!userManager.SupportsUserSecurityStamp) return true;

        var principalStamp = principal.FindFirstValue(
            options.Value.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await userManager.GetSecurityStampAsync(user);
        return principalStamp == userStamp;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var state = await base.GetAuthenticationStateAsync();
        var user = state.User;

        if (user.Identity?.IsAuthenticated != true)
            return state;

        // Get all granted roles from the Identity claims
        var allRoleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (allRoleClaims.Count <= 1)
            return state; // No filtering needed for single-role users

        // Initialize ActiveRoleService with all granted roles
        activeRoleService.Initialize(allRoleClaims);

        // Read the active role from the cookie (set by POST /Account/SwitchRole)
        var cookieRole = httpContextAccessor.HttpContext?.Request.Cookies["ActiveRole"];
        if (!string.IsNullOrEmpty(cookieRole) && allRoleClaims.Contains(cookieRole, StringComparer.OrdinalIgnoreCase))
        {
            activeRoleService.TrySwitch(cookieRole);
        }

        var activeRole = activeRoleService.ActiveRole;

        // Build a new identity with only the active role claim
        var filteredClaims = user.Claims
            .Where(c => c.Type != ClaimTypes.Role)
            .Append(new Claim(ClaimTypes.Role, activeRole))
            .ToList();

        var filteredIdentity = new ClaimsIdentity(
            filteredClaims,
            user.Identity.AuthenticationType,
            ClaimsIdentity.DefaultNameClaimType,
            ClaimTypes.Role);

        return new AuthenticationState(new ClaimsPrincipal(filteredIdentity));
    }
}
