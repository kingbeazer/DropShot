using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IRulesSetService"/>. Mirrors
/// the read+write surface on <c>RulesSetsController</c>.
/// </summary>
public sealed class HttpRulesSetService(HttpClient http) : IRulesSetService
{
    public async Task<List<RulesSetDto>> GetRulesSetsAsync(int? clubId = null, CancellationToken ct = default)
    {
        var url = clubId.HasValue ? $"api/rulessets?clubId={clubId.Value}" : "api/rulessets";
        return await http.GetFromJsonAsync<List<RulesSetDto>>(url, ct) ?? [];
    }

    public Task<RulesSetDetailDto?> GetRulesSetAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<RulesSetDetailDto>($"api/rulessets/{id}", ct);

    public async Task<RulesSetDto?> SaveRulesSetAsync(
        int id, SaveRulesSetRequest request, CancellationToken ct = default)
    {
        var resp = id == 0
            ? await http.PostAsJsonAsync("api/rulessets", request, ct)
            : await http.PutAsJsonAsync($"api/rulessets/{id}", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RulesSetDto>(cancellationToken: ct);
    }

    public async Task DeleteRulesSetAsync(int id, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/rulessets/{id}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<RulesSetItemDto> AddRulesSetItemAsync(
        int rulesSetId, AddRulesSetItemRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/rulessets/{rulesSetId}/items", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RulesSetItemDto>(cancellationToken: ct))!;
    }

    public async Task<RulesSetItemDto> UpdateRulesSetItemAsync(
        int rulesSetId, int itemId, AddRulesSetItemRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/rulessets/{rulesSetId}/items/{itemId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RulesSetItemDto>(cancellationToken: ct))!;
    }

    public async Task DeleteRulesSetItemAsync(int rulesSetId, int itemId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/rulessets/{rulesSetId}/items/{itemId}", ct);
        resp.EnsureSuccessStatusCode();
    }
}
