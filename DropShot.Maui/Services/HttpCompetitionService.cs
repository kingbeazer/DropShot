using System.Net.Http.Json;
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
}
