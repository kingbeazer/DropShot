using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IUserService"/>. Talks to the
/// <c>/api/users</c> endpoint family added in phase 5 alongside the
/// Admin/UserManagement page move.
/// </summary>
public sealed class HttpUserService(HttpClient http) : IUserService
{
    public async Task<List<UserManagementRowDto>> GetAllAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<UserManagementRowDto>>("api/users", ct) ?? [];

    public async Task SetRoleAsync(string userId, string role, bool granted, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/users/{userId}/role",
            new SetUserRoleRequest(role, granted), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SetPremiumAsync(string userId, bool isSubscribed, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/users/{userId}/premium",
            new SetUserPremiumRequest(isSubscribed), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"api/users/{userId}", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string userId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/users/{userId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task AddClubAdminAsync(string userId, int clubId, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/users/{userId}/club-admins",
            new AddClubAdminRequest(clubId), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RemoveClubAdminAsync(string userId, int clubId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/users/{userId}/club-admins/{clubId}", ct);
        resp.EnsureSuccessStatusCode();
    }
}
