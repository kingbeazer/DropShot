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
}
