using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IMatchScoringService"/>. Phase 7
/// prep PR for the TennisScore.razor migration; the page-move PR (7e)
/// switches the page over to this service.
/// </summary>
public sealed class HttpMatchScoringService(HttpClient http) : IMatchScoringService
{
    public async Task<TennisScoreBootstrapDto> GetBootstrapAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<TennisScoreBootstrapDto>(
            "api/match-scoring/bootstrap", ct)
        ?? new TennisScoreBootstrapDto(null, null, [], []);

    public async Task<SavedMatchResumeDto?> GetSavedMatchForResumeAsync(
        int savedMatchId, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(
            $"api/match-scoring/saved-match/{savedMatchId}/resume", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedMatchResumeDto>(cancellationToken: ct);
    }

    public async Task<TennisScoreFixtureContextDto?> GetFixtureContextAsync(
        int fixtureId, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(
            $"api/match-scoring/fixtures/{fixtureId}/scoring-context", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TennisScoreFixtureContextDto>(cancellationToken: ct);
    }

    public async Task<TennisScoreRubberContextDto?> GetRubberContextAsync(
        int rubberId, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(
            $"api/match-scoring/rubbers/{rubberId}/scoring-context", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TennisScoreRubberContextDto>(cancellationToken: ct);
    }

    public async Task<List<ScoringCourtDto>> GetAvailableCourtsAsync(
        int? selectedCourtId, CancellationToken ct = default)
    {
        var qs = selectedCourtId.HasValue ? $"?selectedCourtId={selectedCourtId.Value}" : "";
        return await http.GetFromJsonAsync<List<ScoringCourtDto>>(
            $"api/match-scoring/courts/available{qs}", ct) ?? [];
    }

    private sealed record SavePreferredGameScoringRequest(bool GameScoring);

    public async Task SavePreferredGameScoringAsync(bool gameScoring, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            "api/match-scoring/preferences/game-scoring",
            new SavePreferredGameScoringRequest(gameScoring), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SendFriendRequestAsync(int targetPlayerId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/match-scoring/friends/{targetPlayerId}/request", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    private sealed record UpsertLiveMatchResponse(int SavedMatchId);

    public async Task<int> UpsertLiveMatchAsync(
        UpsertLiveMatchRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/match-scoring/live-match", request, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<UpsertLiveMatchResponse>(cancellationToken: ct);
        return body?.SavedMatchId ?? 0;
    }

    public async Task FinaliseLiveFixtureAsync(
        int fixtureId, FinaliseLiveFixtureRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/match-scoring/fixtures/{fixtureId}/finalise-live", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DiscardLiveMatchAsync(
        int savedMatchId, string? deviceToken, CancellationToken ct = default)
    {
        var qs = string.IsNullOrEmpty(deviceToken)
            ? ""
            : $"?deviceToken={Uri.EscapeDataString(deviceToken)}";
        var resp = await http.DeleteAsync($"api/match-scoring/live-match/{savedMatchId}{qs}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<int> CreateLadderFixtureAsync(
        CreateLadderFixtureRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/match-scoring/ladder-fixture", request, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreateLadderFixtureResponse>(cancellationToken: ct);
        return body?.FixtureId ?? 0;
    }
}
