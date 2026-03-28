using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Components.Authorization;

namespace DropShot.Maui.Services;

/// <summary>
/// Handles login/logout and stores the JWT in SecureStorage.
/// Also acts as the AuthenticationStateProvider for Blazor.
/// </summary>
public class AuthService : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private LoginResponse? _session;

    public AuthService(HttpClient http) => _http = http;

    public LoginResponse? Session => _session;
    public bool IsAdmin => _session?.Roles.Contains("Admin") ?? false;
    public bool IsClubAdmin => _session?.Roles.Contains("ClubAdmin") ?? false;
    public List<int> AdminClubIds => _session?.AdminClubIds ?? [];

    public bool CanEditClub(int clubId) =>
        IsAdmin || AdminClubIds.Contains(clubId);

    public bool CanEditCompetition(int? hostClubId) =>
        IsAdmin || (hostClubId.HasValue && AdminClubIds.Contains(hostClubId.Value));

    // ── Login / Logout ───────────────────────────────────────────────────────

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login",
                new LoginRequest(email, password));

            if (!response.IsSuccessStatusCode) return false;

            _session = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (_session is null) return false;

            // Store token in secure platform storage
            await SecureStorage.SetAsync("access_token", _session.AccessToken);

            // Attach bearer token to all future requests
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _session.AccessToken);

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        _session = null;
        SecureStorage.Remove("access_token");
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        await Task.CompletedTask;
    }

    /// <summary>Attempt to restore a previous session from SecureStorage on app start.</summary>
    public async Task TryRestoreSessionAsync()
    {
        var token = await SecureStorage.GetAsync("access_token");
        if (string.IsNullOrEmpty(token)) return;

        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var info = await _http.GetFromJsonAsync<UserInfoDto>("api/auth/me");
            if (info is null) { await LogoutAsync(); return; }

            _session = new LoginResponse(token, info.UserName, info.Email, info.Roles, info.AdminClubIds);
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
        catch
        {
            await LogoutAsync();
        }
    }

    // ── AuthenticationStateProvider ──────────────────────────────────────────

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_session is null)
            return Task.FromResult(new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity())));

        var claims = ParseClaimsFromJwt(_session.AccessToken);
        var identity = new ClaimsIdentity(claims, "jwt");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.Claims;
    }
}
