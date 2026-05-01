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

    public async Task<List<ClubPlayerDto>> GetClubPlayersAsync(int clubId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ClubPlayerDto>>($"api/clubs/{clubId}/players", ct) ?? [];

    public async Task<List<PlayerDto>> SearchPlayersForClubLinkAsync(int clubId, string term, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PlayerDto>>(
            $"api/clubs/{clubId}/players/search?term={Uri.EscapeDataString(term)}", ct) ?? [];

    public async Task<PlayerDto> CreateLightPlayerAsync(int clubId, CreateLightPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"api/clubs/{clubId}/players/light", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>(cancellationToken: ct))!;
    }

    public async Task<PlayerDto> UpdateLightPlayerAsync(int clubId, int playerId, UpdateLightPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"api/clubs/{clubId}/players/light/{playerId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>(cancellationToken: ct))!;
    }

    public async Task RemovePlayerFromClubAsync(int clubId, int playerId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/clubs/{clubId}/players/{playerId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task LinkExistingPlayerToClubAsync(int clubId, int playerId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/clubs/{clubId}/players/{playerId}/link", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<MyPlayerRowDto>> GetMyPlayersAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<MyPlayerRowDto>>("api/players/mine", ct) ?? [];

    public async Task<PlayerDto> CreateMyLightPlayerAsync(CreateMyLightPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/players/mine", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>(cancellationToken: ct))!;
    }

    public async Task<PlayerDto> UpdateMyLightPlayerAsync(int playerId, UpdateMyLightPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"api/players/mine/{playerId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>(cancellationToken: ct))!;
    }

    public async Task DeleteMyLightPlayerAsync(int playerId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/players/mine/{playerId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task LinkLightToVerifiedAsync(int lightPlayerId, int verifiedPlayerId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/players/mine/{lightPlayerId}/link-to/{verifiedPlayerId}", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<SimilarPlayerDto>> SearchSimilarVerifiedPlayersAsync(string term, int max, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SimilarPlayerDto>>(
            $"api/players/search-similar?term={Uri.EscapeDataString(term)}&max={max}", ct) ?? [];
}
