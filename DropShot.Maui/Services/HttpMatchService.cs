using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IMatchService"/>. Phase 5 — Match
/// landing page reads. Scoring writes land in phase 6.
/// </summary>
public sealed class HttpMatchService(HttpClient http) : IMatchService
{
    public async Task<List<ActiveMatchDto>> GetMyActiveMatchesAsync(
        string? deviceToken, CancellationToken ct = default)
    {
        var qs = string.IsNullOrEmpty(deviceToken)
            ? ""
            : $"?deviceToken={Uri.EscapeDataString(deviceToken)}";
        return await http.GetFromJsonAsync<List<ActiveMatchDto>>(
            $"api/matches/mine{qs}", ct) ?? [];
    }

    public async Task<List<RecentCasualMatchDto>> GetMyRecentCasualMatchesAsync(
        int limit = 6, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<RecentCasualMatchDto>>(
            $"api/matches/casual/recent?limit={limit}", ct) ?? [];
}
