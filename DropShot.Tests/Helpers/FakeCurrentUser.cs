using DropShot.UI.Services.Auth;

namespace DropShot.Tests.Helpers;

/// <summary>
/// Minimal ICurrentUser for service-level tests that only need UserId —
/// avoids standing up a full WebCurrentUser (which needs a live
/// AuthenticationStateProvider from a rendered bUnit context).
/// </summary>
public sealed class FakeCurrentUser(string? userId) : ICurrentUser
{
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public string? ActiveRole => null;
    public IReadOnlyCollection<string> GrantedRoles => [];
    public IReadOnlyCollection<int> AdminClubIds => [];
    public int? ActiveClubId => null;
    public bool IsAuthenticated => UserId is not null;
    public bool IsAdmin => false;
    public bool IsClubAdmin => false;
    public bool IsSubscribed => false;
    public bool IsSuperAdmin => false;
    public bool CanCreateUserCompetition => false;
    public bool CanScoreMatch => false;
    public bool HasRole(string role) => false;
    public bool CanEditClub(int clubId) => false;
    public bool CanEditCompetition(int? hostClubId) => false;
    public event Action? Changed { add { } remove { } }
    public Task EnsureLoadedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
