using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IRulesSetService"/>. Lifts the
/// Rules Sets section of <see cref="ApiService"/>.
/// </summary>
public sealed class HttpRulesSetService(HttpClient http) : IRulesSetService
{
    public async Task<List<RulesSetDto>> GetRulesSetsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RulesSetDto>>("api/rulessets", ct) ?? [];

    public Task<RulesSetDetailDto?> GetRulesSetAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<RulesSetDetailDto>($"api/rulessets/{id}", ct);
}
