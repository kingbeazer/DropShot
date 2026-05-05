using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DropShot.Data;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of ICurrentUser. Snapshots the authenticated principal
/// (after ActiveRoleAuthenticationStateProvider has filtered claims to the active role)
/// plus the ClubAdministrators rows for the user, and rebuilds the snapshot whenever
/// the auth state changes.
/// </summary>
public sealed class WebCurrentUser : ICurrentUser, IDisposable
{
    private readonly AuthenticationStateProvider _authState;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<MyDbContext> _dbFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private string? _userId;
    private string? _userName;
    private string? _email;
    private string? _activeRole;
    private List<string> _grantedRoles = new();
    private List<int> _adminClubIds = new();
    private bool _isAuthenticated;
    private bool _isSubscribed;

    public event Action? Changed;

    public WebCurrentUser(
        AuthenticationStateProvider authState,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<MyDbContext> dbFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _authState = authState;
        _userManager = userManager;
        _dbFactory = dbFactory;
        _httpContextAccessor = httpContextAccessor;
        _authState.AuthenticationStateChanged += OnAuthStateChanged;
        _ = RefreshAsync();
    }

    private void OnAuthStateChanged(Task<AuthenticationState> task) => _ = RefreshFromAsync(task);

    private async Task RefreshFromAsync(Task<AuthenticationState> task)
    {
        try
        {
            await ApplyStateAsync(await task);
            Changed?.Invoke();
        }
        catch
        {
            // Swallow — auth state failure shouldn't crash the circuit
        }
    }

    private async Task RefreshAsync() => await ApplyStateAsync(await _authState.GetAuthenticationStateAsync());

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        // Swallow — ServerAuthenticationStateProvider.GetAuthenticationStateAsync
        // throws InvalidOperationException when the auth-state task on this
        // scope hasn't been initialised (some interactive Blazor nav paths
        // hit OnInitializedAsync before the state is wired up). Callers
        // fall back to the existing snapshot populated by an earlier
        // RefreshAsync; a real auth change still arrives through
        // OnAuthStateChanged.
        try { await RefreshAsync(); } catch { }
    }

    private async Task ApplyStateAsync(AuthenticationState state)
    {
        var user = state.User;
        var newAuthed = user.Identity?.IsAuthenticated == true;

        // Transient unauthenticated states can come through
        // OnAuthStateChanged with an anonymous principal during Blazor
        // navigation in the same circuit (the auth-state task gets
        // re-resolved per call). Blindly clearing the snapshot here
        // makes downstream reads of currentUser.UserId return null and
        // server-side queries (My Competitions etc.) silently return
        // empty until something forces another refresh. Real logouts
        // tear down the circuit, so the next ApplyStateAsync runs with
        // _userId already null and follows the not-authenticated branch.
        if (!newAuthed && _userId is not null)
        {
            return;
        }

        _isAuthenticated = newAuthed;
        if (!_isAuthenticated)
        {
            _userId = null;
            _userName = null;
            _email = null;
            _activeRole = null;
            _grantedRoles = new();
            _adminClubIds = new();
            _isSubscribed = false;
            return;
        }

        // Cookie auth (web) sets NameIdentifier directly. JWT bearer (MAUI) carries
        // the user id under "sub" — which the modern JsonWebTokenHandler does NOT
        // auto-map to NameIdentifier — so fall back to the JWT registered names.
        _userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue("sub");
        _userName = user.Identity?.Name
                    ?? user.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
        _activeRole = user.FindFirstValue(ClaimTypes.Role)
                      ?? user.FindFirstValue("active_role");

        if (_userId is null) return;

        var appUser = await _userManager.FindByIdAsync(_userId);
        if (appUser is not null)
        {
            _email = appUser.Email;
            _grantedRoles = (await _userManager.GetRolesAsync(appUser)).ToList();
            _isSubscribed = appUser.IsSubscribed;
        }
        else
        {
            _isSubscribed = false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        _adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == _userId)
            .Select(ca => ca.ClubId)
            .ToListAsync();
    }

    /// <summary>
    /// API requests authenticated via JWT bearer (MAUI / iOS) populate
    /// HttpContext.User but never feed the Blazor AuthenticationStateProvider
    /// (which only tracks SignalR circuits) — and ServerAuthenticationStateProvider
    /// throws on circuits that were never initialised, faulting the constructor's
    /// fire-and-forget RefreshAsync so the snapshot fields stay null. Read the
    /// principal-derived fields straight off HttpContext.User when one is present,
    /// and fall back to the Blazor snapshot for interactive Blazor pages
    /// (where HttpContext is null during SignalR mode).
    /// </summary>
    private ClaimsPrincipal? ApiPrincipal
    {
        get
        {
            var u = _httpContextAccessor.HttpContext?.User;
            return u?.Identity?.IsAuthenticated == true ? u : null;
        }
    }

    public string? UserId =>
        ApiPrincipal is { } u
            ? u.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? u.FindFirstValue(JwtRegisteredClaimNames.Sub)
              ?? u.FindFirstValue("sub")
            : _userId;

    public string? UserName =>
        ApiPrincipal is { } u
            ? u.Identity?.Name ?? u.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            : _userName;

    public string? Email => _email;

    public string? ActiveRole =>
        ApiPrincipal is { } u
            ? u.FindFirstValue(ClaimTypes.Role) ?? u.FindFirstValue("active_role")
            : _activeRole;
    public IReadOnlyCollection<string> GrantedRoles => _grantedRoles;
    public IReadOnlyCollection<int> AdminClubIds => _adminClubIds;

    public int? ActiveClubId
    {
        get
        {
            if (_adminClubIds.Count == 0) return null;
            if (int.TryParse(_httpContextAccessor.HttpContext?.Request.Cookies["ActiveClubId"], out var cookieClubId)
                && _adminClubIds.Contains(cookieClubId))
            {
                return cookieClubId;
            }
            return _adminClubIds[0];
        }
    }

    public bool IsAuthenticated => ApiPrincipal is not null || _isAuthenticated;
    public bool IsAdmin => _activeRole is "Admin" or "SuperAdmin";
    public bool IsClubAdmin => _activeRole == "ClubAdmin";
    public bool IsSubscribed => _isSubscribed;

    public bool HasRole(string role) =>
        _grantedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool CanEditClub(int clubId) =>
        IsAdmin || _adminClubIds.Contains(clubId);

    public bool CanEditCompetition(int? hostClubId) =>
        IsAdmin || (hostClubId.HasValue && _adminClubIds.Contains(hostClubId.Value));

    public bool CanCreateUserCompetition
    {
        get
        {
            if (HasRole("SuperAdmin") || HasRole("Admin")) return true;
            // Acting as plain User (active role) AND subscribed. Mirrors
            // ClubAuthorizationService.CanCreateUserCompetition, which gates
            // on the *active* role — a ClubAdmin who's toggled into User
            // mode and subscribed is allowed too.
            //
            // Use the _activeRole *field* (set from the filtered Blazor
            // AuthenticationState) rather than the ActiveRole *property*:
            // during prerender the property reads from HttpContext.User,
            // which carries every granted Role claim, so for multi-role
            // accounts FindFirstValue(Role) is whichever claim happens to
            // be first in DB order — not the role the user is currently
            // acting as.
            return _activeRole is "User" && _isSubscribed;
        }
    }

    public void Dispose()
    {
        _authState.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
