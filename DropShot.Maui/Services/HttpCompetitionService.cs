using System.Net.Http.Json;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="ICompetitionService"/>. Lifts the
/// Competitions section of <see cref="ApiService"/>.
/// </summary>
public sealed class HttpCompetitionService(HttpClient http) : ICompetitionService
{
    public async Task<List<CompetitionDto>> GetCompetitionsAsync(bool includeArchived = false, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CompetitionDto>>(
            $"api/competitions?includeArchived={includeArchived}", ct) ?? [];

    public Task<CompetitionDetailDto?> GetCompetitionAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<CompetitionDetailDto>($"api/competitions/{id}", ct);

    public async Task SelfRegisterAsync(int competitionId, ParticipantStatus status, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/{competitionId}/self-register",
            new UpdateParticipantStatusRequest(status), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ConfirmParticipationAsync(int competitionId, ParticipantStatus status, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/{competitionId}/confirm-participation",
            new UpdateParticipantStatusRequest(status), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ApproveFixtureResultAsync(int fixtureId, ApproveFixtureResultRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/fixtures/{fixtureId}/approve-result", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SubmitFixtureScoreAsync(int fixtureId, SubmitFixtureScoreRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/fixtures/{fixtureId}/submit-score", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<MyCompetitionsViewDto> GetMyCompetitionsViewAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MyCompetitionsViewDto>("api/competitions/my-view", ct)
            ?? new MyCompetitionsViewDto(false, [], []);

    public async Task<List<CompetitionFixtureDto>> GetPendingVerificationFixturesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CompetitionFixtureDto>>("api/competitions/pending-verification", ct) ?? [];

    public async Task ToggleArchiveAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/competitions/{competitionId}/toggle-archive", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCompetitionAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/competitions/{competitionId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task EnterCompetitionAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/competitions/{competitionId}/enter", null, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to enter." : body);
        }
    }
}
