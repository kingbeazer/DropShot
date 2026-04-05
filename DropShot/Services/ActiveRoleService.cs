namespace DropShot.Services;

/// <summary>
/// Scoped service that tracks the active role for the current Blazor circuit.
/// Each user session gets its own instance. The active role defaults to the
/// highest-privilege granted role on first access.
/// </summary>
public class ActiveRoleService
{
    private string? _activeRole;
    private List<string> _grantedRoles = [];

    public event Action? OnChange;

    /// <summary>All roles the user has been granted via ASP.NET Identity.</summary>
    public IReadOnlyList<string> GrantedRoles => _grantedRoles;

    /// <summary>The currently active role used for authorization checks.</summary>
    public string ActiveRole => _activeRole ?? (_grantedRoles.Count > 0 ? _grantedRoles[0] : "");

    /// <summary>True when the user has more than one granted role (show role switcher).</summary>
    public bool CanSwitchRole => _grantedRoles.Count > 1;

    /// <summary>
    /// Initialize with the user's granted roles. Ordered by privilege descending:
    /// SuperAdmin > Admin > ClubAdmin > others.
    /// </summary>
    public void Initialize(IList<string> grantedRoles)
    {
        _grantedRoles = grantedRoles
            .OrderBy(r => r switch
            {
                "SuperAdmin" => 0,
                "Admin" => 1,
                "ClubAdmin" => 2,
                _ => 3
            })
            .ToList();

        // Default to highest-privilege role
        _activeRole ??= _grantedRoles.FirstOrDefault();
    }

    /// <summary>
    /// Switch to a different role. Returns false if the role is not in granted roles.
    /// </summary>
    public bool TrySwitch(string newRole)
    {
        if (!_grantedRoles.Contains(newRole, StringComparer.OrdinalIgnoreCase))
            return false;

        _activeRole = _grantedRoles.First(r => r.Equals(newRole, StringComparison.OrdinalIgnoreCase));
        OnChange?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns the previous active role (before the last switch), or the current one if not yet switched.
    /// Stored transiently to support logging.
    /// </summary>
    public string PreviousRole { get; set; } = "";
}
