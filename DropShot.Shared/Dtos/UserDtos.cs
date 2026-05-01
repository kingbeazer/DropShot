namespace DropShot.Shared.Dtos;

/// <summary>
/// Row in the Admin/UserManagement table. Combines the ApplicationUser fields
/// the page needs with role flags and the user's club-admin assignments.
/// </summary>
public record UserManagementRowDto(
    string Id,
    string? UserName,
    string? Email,
    string? DisplayName,
    bool IsSuperAdmin,
    bool IsAdmin,
    bool IsSubscribed,
    IReadOnlyList<ClubAdminAssignmentDto> ClubAssignments);

public record ClubAdminAssignmentDto(int ClubId, string ClubName);

public record UpdateUserRequest(string UserName, string Email);

public record SetUserRoleRequest(string Role, bool Granted);

public record SetUserPremiumRequest(bool IsSubscribed);

public record AddClubAdminRequest(int ClubId);
