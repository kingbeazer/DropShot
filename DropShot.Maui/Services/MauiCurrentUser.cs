using System.Security.Claims;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI implementation of ICurrentUser. Thin adapter over the existing
/// JWT-backed AuthService; UserId is parsed once per auth-state change from
/// the JWT (sub claim → NameIdentifier under default JwtSecurityTokenHandler mapping).
/// </summary>
public sealed class MauiCurrentUser : ICurrentUser, IDisposable
{
    private readonly AuthService _auth;
    private string? _userId;

    public event Action? Changed;

    public MauiCurrentUser(AuthService auth)
    {
        _auth = auth;
        _auth.AuthenticationStateChanged += OnAuthStateChanged;
        _ = RefreshAsync();
    }

    private void OnAuthStateChanged(Task<AuthenticationState> task) => _ = RefreshFromAsync(task);

    private async Task RefreshFromAsync(Task<AuthenticationState> task)
    {
        try
        {
            var state = await task;
            _userId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? state.User.FindFirst("sub")?.Value;
            Changed?.Invoke();
        }
        catch
        {
            // Swallow — auth state failure shouldn't crash the circuit
        }
    }

    private async Task RefreshAsync()
    {
        var state = await _auth.GetAuthenticationStateAsync();
        _userId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? state.User.FindFirst("sub")?.Value;
    }

    public string? UserId => _userId;
    public string? UserName => _auth.Session?.UserName;
    public string? Email => _auth.Session?.Email;
    public string? ActiveRole => _auth.Session is null ? null : _auth.ActiveRole;
    public IReadOnlyCollection<string> GrantedRoles => _auth.GrantedRoles;
    public IReadOnlyCollection<int> AdminClubIds => _auth.AdminClubIds;

    public int? ActiveClubId =>
        _auth.AdminClubIds.Count == 1 ? _auth.AdminClubIds.First() : null;

    public bool IsAuthenticated => _auth.Session is not null;
    public bool IsAdmin => _auth.IsAdmin;
    public bool IsClubAdmin => _auth.IsClubAdmin;
    public bool IsSuperAdmin =>
        string.Equals(_auth.ActiveRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

    // The login DTO doesn't carry the subscription bit yet, so on MAUI we
    // conservatively report false. The "user competition" creation path is a
    // web-only feature until that plumbing lands.
    public bool IsSubscribed => false;

    public bool HasRole(string role) =>
        _auth.GrantedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool CanEditClub(int clubId) => _auth.CanEditClub(clubId);

    public bool CanEditCompetition(int? hostClubId) => _auth.CanEditCompetition(hostClubId);

    public bool CanCreateUserCompetition => IsAdmin;

    public Task EnsureLoadedAsync(CancellationToken ct = default) => RefreshAsync();

    public void Dispose()
    {
        _auth.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
