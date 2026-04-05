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
/// based on the user's active role. This ensures [Authorize(Roles=...)] and
/// AuthorizeView automatically respect the active role selection.
/// </summary>
internal sealed class ActiveRoleAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options,
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

        // Initialize the active role service with all granted roles
        var allRoleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (allRoleClaims.Count > 0)
            activeRoleService.Initialize(allRoleClaims);

        // If there's only one role (or none), no filtering needed
        if (allRoleClaims.Count <= 1)
            return state;

        // Build a new identity with only the active role claim
        var activeRole = activeRoleService.ActiveRole;
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

    /// <summary>
    /// Call this after a role switch to force all Blazor components
    /// to re-evaluate authorization.
    /// </summary>
    public void NotifyRoleChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
