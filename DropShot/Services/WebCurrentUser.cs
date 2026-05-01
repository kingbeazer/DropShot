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

    private async Task ApplyStateAsync(AuthenticationState state)
    {
        var user = state.User;
        _isAuthenticated = user.Identity?.IsAuthenticated == true;
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

        _userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        _userName = user.Identity?.Name;
        _activeRole = user.FindFirstValue(ClaimTypes.Role);

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

    public string? UserId => _userId;
    public string? UserName => _userName;
    public string? Email => _email;
    public string? ActiveRole => _activeRole;
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

    public bool IsAuthenticated => _isAuthenticated;
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
            // Plain User mode AND subscribed
            return _grantedRoles.Count == 1
                && _grantedRoles[0].Equals("User", StringComparison.OrdinalIgnoreCase)
                && _isSubscribed;
        }
    }

    public void Dispose()
    {
        _authState.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
