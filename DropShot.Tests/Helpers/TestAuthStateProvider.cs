using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace DropShot.Tests.Helpers;

public class TestAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public TestAuthStateProvider(
        bool authenticated = false,
        string userId = "test-user-id",
        string userName = "test@example.com",
        string[]? roles = null)
    {
        if (!authenticated)
        {
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, userName),
        };

        foreach (var role in roles ?? [])
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_state);
}
