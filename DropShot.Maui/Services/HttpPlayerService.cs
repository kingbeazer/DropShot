using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IPlayerService"/>. Lifts the Players
/// section of <see cref="ApiService"/>; the god-class is intentionally
/// duplicated through phases 3–7 and deleted in phase 8 once no callers remain.
/// </summary>
public sealed class HttpPlayerService(HttpClient http) : IPlayerService
{
    public async Task<List<PlayerDto>> GetPlayersAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PlayerDto>>("api/players", ct) ?? [];

    public Task<PlayerDto?> GetPlayerAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<PlayerDto>($"api/players/{id}", ct);

    public async Task<List<GlobalLeagueTableEntryDto>> GetGlobalLeagueTableAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<GlobalLeagueTableEntryDto>>("api/players/league-table", ct) ?? [];

    public async Task<List<PlayerWithClubsDto>> GetPlayersWithClubsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PlayerWithClubsDto>>("api/players/with-clubs", ct) ?? [];

    public async Task<PlayerDto> CreatePlayerAsync(CreatePlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/players", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>(cancellationToken: ct))!;
    }

    public async Task<PlayerDto> UpdatePlayerAsync(int playerId, UpdatePlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"api/players/{playerId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>(cancellationToken: ct))!;
    }

    public async Task DeletePlayerAsync(int playerId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/players/{playerId}", ct);
        resp.EnsureSuccessStatusCode();
    }
}
