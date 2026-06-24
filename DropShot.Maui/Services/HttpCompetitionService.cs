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

    public async Task SelfRegisterAsync(
        int competitionId, ParticipantStatus status, PhoneShareConsent consent, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/{competitionId}/self-register",
            new SelfRegisterRequest(status, consent), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ConfirmParticipationAsync(
        int competitionId, ParticipantStatus status, PhoneShareConsent consent, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/{competitionId}/confirm-participation",
            new SelfRegisterRequest(status, consent), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ApproveFixtureResultAsync(int fixtureId, ApproveFixtureResultRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/fixtures/{fixtureId}/approve-result", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<LadderSimulationResultDto> SimulateLadderAsync(
        int competitionId, int weeks, int? seed = null, CancellationToken ct = default)
    {
        var qs = seed.HasValue ? $"?weeks={weeks}&seed={seed}" : $"?weeks={weeks}";
        var resp = await http.PostAsync($"api/competitions/{competitionId}/ladder/simulate{qs}", content: null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LadderSimulationResultDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty simulation response.");
    }

    public async Task SubmitFixtureScoreAsync(int fixtureId, SubmitFixtureScoreRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/fixtures/{fixtureId}/submit-score", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<FixtureScoreContextDto?> GetFixtureScoreContextAsync(int fixtureId, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"api/competitions/fixtures/{fixtureId}/score-context", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("You can't score this fixture.");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<FixtureScoreContextDto>(cancellationToken: ct);
    }

    public async Task<FixtureScoreContextDto?> GetFixtureScoreContextByTokenAsync(Guid token, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"api/competitions/fixtures/by-token/{token}/score-context", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<FixtureScoreContextDto>(cancellationToken: ct);
    }

    public async Task SubmitFixtureScoreByTokenAsync(Guid token, SubmitFixtureScoreRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"api/competitions/fixtures/by-token/{token}/submit-score", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<MyCompetitionsViewDto> GetMyCompetitionsViewAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MyCompetitionsViewDto>("api/competitions/my-view", ct)
            ?? new MyCompetitionsViewDto(false, [], []);

    public async Task<List<CompetitionFixtureDto>> GetPendingVerificationFixturesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CompetitionFixtureDto>>("api/competitions/pending-verification", ct) ?? [];

    public async Task<List<CompetitionFixtureDto>> GetMyUpcomingFixturesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CompetitionFixtureDto>>(
            "api/competitions/mine/upcoming-fixtures", ct) ?? [];

    public async Task<List<CompetitionFixtureDto>> GetMyRecentCompletedFixturesAsync(
        int limit = 6, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CompetitionFixtureDto>>(
            $"api/competitions/mine/recent-fixtures?limit={limit}", ct) ?? [];

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

    public async Task EnterCompetitionAsync(
        int competitionId,
        PhoneShareConsent consent,
        ParticipantStatus status = ParticipantStatus.FullPlayer,
        CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/{competitionId}/enter",
            new EnterCompetitionRequest(consent, status), ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to enter." : body);
        }
    }

    public async Task LeaveCompetitionAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/competitions/{competitionId}/leave", null, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to leave." : body);
        }
    }

    public Task<FixtureRubberContextDto?> GetFixtureRubberContextAsync(int fixtureId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<FixtureRubberContextDto>(
            $"api/competitions/fixtures/{fixtureId}/rubber-context", ct);

    public async Task SubmitRubberScoresAsync(
        int fixtureId, SubmitRubberScoresRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/fixtures/{fixtureId}/submit-rubber-scores", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ClearRubberScoreAsync(int fixtureId, int rubberId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/fixtures/{fixtureId}/rubbers/{rubberId}/clear-score", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task EnsureFixtureRubbersAsync(int fixtureId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/fixtures/{fixtureId}/ensure-rubbers", null, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to ensure rubbers." : body);
        }
    }

    public async Task<VerifyFixtureViewDto?> GetFixtureForVerificationAsync(Guid token, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync($"api/competitions/verify/{token}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VerifyFixtureViewDto>(cancellationToken: ct);
    }

    public async Task<ApproveFixtureByTokenResultDto> ApproveFixtureByTokenAsync(
        Guid token, ApproveFixtureByTokenRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/verify/{token}/approve", request, ct);
        if (!resp.IsSuccessStatusCode)
            return new ApproveFixtureByTokenResultDto(false,
                "Failed to approve. Please try again.", null, false);
        return (await resp.Content.ReadFromJsonAsync<ApproveFixtureByTokenResultDto>(cancellationToken: ct))
            ?? new ApproveFixtureByTokenResultDto(false, "Failed to approve.", null, false);
    }
}
