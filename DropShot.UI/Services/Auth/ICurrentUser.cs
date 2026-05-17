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
    bool IsSubscribed { get; }
    /// <summary>
    /// True when the user's <em>active</em> role is SuperAdmin. Distinct from
    /// <c>HasRole("SuperAdmin")</c>, which would also return true for a
    /// SuperAdmin who has switched to a lower-privilege role for browsing
    /// purposes.
    /// </summary>
    bool IsSuperAdmin { get; }
    bool HasRole(string role);
    bool CanEditClub(int clubId);
    bool CanEditCompetition(int? hostClubId);

    /// <summary>
    /// Whether the user can create a competition without a host club. Mirrors
    /// <c>ClubAuthorizationService.CanCreateUserCompetition</c> for the UI:
    /// admin/superadmin always; otherwise only when the active role is "User"
    /// and the user has an active subscription.
    /// </summary>
    bool CanCreateUserCompetition { get; }

    /// <summary>Raised after the underlying auth state changes (login, logout, role switch, session restore).</summary>
    event Action? Changed;

    /// <summary>
    /// Forces the user-state snapshot (UserId, ActiveRole, GrantedRoles,
    /// AdminClubIds) to be populated before property reads return stable
    /// values. Pages call this in OnInitializedAsync before branching on
    /// IsAdmin / AdminClubIds / CanEditCompetition so they don't race the
    /// constructor's fire-and-forget snapshot load. Idempotent — safe to
    /// call repeatedly.
    /// </summary>
    Task EnsureLoadedAsync(CancellationToken ct = default);
}
