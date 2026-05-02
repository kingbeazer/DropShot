using System.Net.Http.Json;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="ICourtClaimService"/>. Phase 7 prep
/// PR — first MAUI-side implementation of the court-claim surface (the web
/// backend has had it since Phase 6). Backs the live-scoring page (PR 7e)
/// and the Match landing page on MAUI.
/// </summary>
public sealed class HttpCourtClaimService(HttpClient http) : ICourtClaimService
{
    public async Task<bool> IsStaleAsync(int savedMatchId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<bool>(
            $"api/court-claim/saved-match/{savedMatchId}/is-stale", ct);

    public async Task ExtendGraceAsync(int savedMatchId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/court-claim/saved-match/{savedMatchId}/extend-grace", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task EndMatchAsync(int savedMatchId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/court-claim/saved-match/{savedMatchId}/end", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<ActiveMatchDto?> GetUserActiveMatchAsync(
        string userId, int? excludingSavedMatchId = null, CancellationToken ct = default)
    {
        var qs = excludingSavedMatchId.HasValue
            ? $"?excludingSavedMatchId={excludingSavedMatchId.Value}"
            : "";
        using var resp = await http.GetAsync($"api/court-claim/active{qs}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ActiveMatchDto>(cancellationToken: ct);
    }

    public async Task<CourtClaimResult> EvaluateAsync(int courtId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<CourtClaimResult>(
            $"api/court-claim/courts/{courtId}/evaluate", ct)
        ?? CourtClaimResult.Free();
}
