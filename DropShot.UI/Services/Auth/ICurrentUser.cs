namespace DropShot.UI.Services.Auth;

/// <summary>
/// Host-agnostic view of the currently authenticated user.
/// Web implementation reads from ASP.NET Identity + cookies + ActiveRoleService;
/// MAUI implementation adapts the JWT-backed AuthService.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    string? ActiveRole { get; }
    IReadOnlyCollection<string> GrantedRoles { get; }
    IReadOnlyCollection<int> AdminClubIds { get; }

    /// <summary>
    /// The club the user is currently "acting as administrator" for.
    /// Web: reads the ActiveClubId cookie; MAUI: the sole admin club if exactly one exists.
    /// Null when the user administrates no clubs, or (on MAUI) administrates more than one.
    /// </summary>
    int? ActiveClubId { get; }

    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsClubAdmin { get; }
    bool HasRole(string role);
    bool CanEditClub(int clubId);
    bool CanEditCompetition(int? hostClubId);

    /// <summary>Raised after the underlying auth state changes (login, logout, role switch, session restore).</summary>
    event Action? Changed;
}
