using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IScoreboardService"/>. Hits the
/// new /api/scoreboard endpoints; live updates flow through SignalR via
/// <see cref="IScoreboardHubFactory"/>.
/// </summary>
public sealed class HttpScoreboardService(HttpClient http) : IScoreboardService
{
    public async Task<List<ScoreboardCourtDto>> GetAdminCourtsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ScoreboardCourtDto>>("api/scoreboard/courts", ct) ?? [];

    public async Task<ScoreboardCourtStateDto> GetCourtStateAsync(int courtId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<ScoreboardCourtStateDto>($"api/scoreboard/courts/{courtId}/state", ct)
            ?? throw new InvalidOperationException("Court state response was empty.");

    public async Task UpdateDisplaySettingAsync(int courtId, UpdateDisplaySettingRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/scoreboard/courts/{courtId}/display-setting", request, ct);
        resp.EnsureSuccessStatusCode();
    }
}
