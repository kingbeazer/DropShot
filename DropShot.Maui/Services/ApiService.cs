using System.Net.Http.Json;
using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.Maui.Services;

/// <summary>
/// Typed wrapper around HttpClient for all DropShot API calls.
/// </summary>
public class ApiService(HttpClient http)
{
    // ── Players ──────────────────────────────────────────────────────────────

    public async Task<List<PlayerDto>> GetPlayersAsync() =>
        await http.GetFromJsonAsync<List<PlayerDto>>("api/players") ?? [];

    public Task<PlayerDto?> GetPlayerAsync(int id) =>
        http.GetFromJsonAsync<PlayerDto>($"api/players/{id}");

    public async Task<PlayerDto?> CreatePlayerAsync(CreatePlayerRequest req)
    {
        var r = await http.PostAsJsonAsync("api/players", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<PlayerDto>();
    }

    public async Task<PlayerDto?> UpdatePlayerAsync(int id, UpdatePlayerRequest req)
    {
        var r = await http.PutAsJsonAsync($"api/players/{id}", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<PlayerDto>();
    }

    public async Task DeletePlayerAsync(int id)
    {
        var r = await http.DeleteAsync($"api/players/{id}");
        r.EnsureSuccessStatusCode();
    }

    // ── Competitions ─────────────────────────────────────────────────────────

    public async Task<List<CompetitionDto>> GetCompetitionsAsync() =>
        await http.GetFromJsonAsync<List<CompetitionDto>>("api/competitions") ?? [];

    public Task<CompetitionDetailDto?> GetCompetitionAsync(int id) =>
        http.GetFromJsonAsync<CompetitionDetailDto>($"api/competitions/{id}");

    public async Task<CompetitionDto?> SaveCompetitionAsync(int id, SaveCompetitionRequest req)
    {
        HttpResponseMessage r = id == 0
            ? await http.PostAsJsonAsync("api/competitions", req)
            : await http.PutAsJsonAsync($"api/competitions/{id}", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompetitionDto>();
    }

    public async Task DeleteCompetitionAsync(int id)
    {
        var r = await http.DeleteAsync($"api/competitions/{id}");
        r.EnsureSuccessStatusCode();
    }

    public async Task AddStageAsync(int competitionId, AddStageRequest req)
    {
        var r = await http.PostAsJsonAsync($"api/competitions/{competitionId}/stages", req);
        r.EnsureSuccessStatusCode();
    }

    public async Task DeleteStageAsync(int competitionId, int stageId)
    {
        var r = await http.DeleteAsync($"api/competitions/{competitionId}/stages/{stageId}");
        r.EnsureSuccessStatusCode();
    }

    public async Task AddParticipantAsync(int competitionId, int playerId)
    {
        var r = await http.PostAsJsonAsync($"api/competitions/{competitionId}/participants",
            new AddParticipantRequest(playerId));
        r.EnsureSuccessStatusCode();
    }

    public async Task UpdateParticipantStatusAsync(int competitionId, int playerId, ParticipantStatus status)
    {
        var r = await http.PutAsJsonAsync(
            $"api/competitions/{competitionId}/participants/{playerId}",
            new UpdateParticipantStatusRequest(status));
        r.EnsureSuccessStatusCode();
    }

    public async Task RemoveParticipantAsync(int competitionId, int playerId)
    {
        var r = await http.DeleteAsync($"api/competitions/{competitionId}/participants/{playerId}");
        r.EnsureSuccessStatusCode();
    }

    // ── Clubs ─────────────────────────────────────────────────────────────────

    public async Task<List<ClubDto>> GetClubsAsync() =>
        await http.GetFromJsonAsync<List<ClubDto>>("api/clubs") ?? [];

    public Task<ClubDetailDto?> GetClubAsync(int id) =>
        http.GetFromJsonAsync<ClubDetailDto>($"api/clubs/{id}");

    public async Task<ClubDto?> SaveClubAsync(int id, SaveClubRequest req)
    {
        HttpResponseMessage r = id == 0
            ? await http.PostAsJsonAsync("api/clubs", req)
            : await http.PutAsJsonAsync($"api/clubs/{id}", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<ClubDto>();
    }

    public async Task DeleteClubAsync(int id)
    {
        var r = await http.DeleteAsync($"api/clubs/{id}");
        r.EnsureSuccessStatusCode();
    }

    public async Task<CourtDto?> AddCourtAsync(int clubId, AddCourtRequest req)
    {
        var r = await http.PostAsJsonAsync($"api/clubs/{clubId}/courts", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CourtDto>();
    }

    public async Task<CourtDto?> UpdateCourtAsync(int clubId, int courtId, UpdateCourtRequest req)
    {
        var r = await http.PutAsJsonAsync($"api/clubs/{clubId}/courts/{courtId}", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CourtDto>();
    }

    public async Task DeleteCourtAsync(int clubId, int courtId)
    {
        var r = await http.DeleteAsync($"api/clubs/{clubId}/courts/{courtId}");
        r.EnsureSuccessStatusCode();
    }

    // ── Rules Sets ────────────────────────────────────────────────────────────

    public async Task<List<RulesSetDto>> GetRulesSetsAsync() =>
        await http.GetFromJsonAsync<List<RulesSetDto>>("api/rulessets") ?? [];

    public Task<RulesSetDetailDto?> GetRulesSetAsync(int id) =>
        http.GetFromJsonAsync<RulesSetDetailDto>($"api/rulessets/{id}");

    public async Task<RulesSetDto?> SaveRulesSetAsync(int id, SaveRulesSetRequest req)
    {
        HttpResponseMessage r = id == 0
            ? await http.PostAsJsonAsync("api/rulessets", req)
            : await http.PutAsJsonAsync($"api/rulessets/{id}", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<RulesSetDto>();
    }

    public async Task DeleteRulesSetAsync(int id)
    {
        var r = await http.DeleteAsync($"api/rulessets/{id}");
        r.EnsureSuccessStatusCode();
    }

    public async Task AddRulesSetItemAsync(int rulesSetId, string ruleText)
    {
        var r = await http.PostAsJsonAsync($"api/rulessets/{rulesSetId}/items",
            new AddRulesSetItemRequest(ruleText));
        r.EnsureSuccessStatusCode();
    }

    public async Task DeleteRulesSetItemAsync(int rulesSetId, int itemId)
    {
        var r = await http.DeleteAsync($"api/rulessets/{rulesSetId}/items/{itemId}");
        r.EnsureSuccessStatusCode();
    }
}
