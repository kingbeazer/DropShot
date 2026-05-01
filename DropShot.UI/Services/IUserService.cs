using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// User-management domain abstraction. Backs Admin/UserManagement (phase 5).
/// All operations are gated for Admin / SuperAdmin server-side; the role
/// toggles + premium toggle are SuperAdmin-only at the controller level.
/// </summary>
public interface IUserService
{
    Task<List<UserManagementRowDto>> GetAllAsync(CancellationToken ct = default);
    Task SetRoleAsync(string userId, string role, bool granted, CancellationToken ct = default);
    Task SetPremiumAsync(string userId, bool isSubscribed, CancellationToken ct = default);
    Task UpdateAsync(string userId, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(string userId, CancellationToken ct = default);
    Task AddClubAdminAsync(string userId, int clubId, CancellationToken ct = default);
    Task RemoveClubAdminAsync(string userId, int clubId, CancellationToken ct = default);
}
