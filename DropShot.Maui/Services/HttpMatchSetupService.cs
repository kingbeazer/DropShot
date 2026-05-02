using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IMatchSetupService"/>. Phase 7 PR
/// 7e: backs the MatchSetupWizard once it lives in the shared RCL.
/// </summary>
public sealed class HttpMatchSetupService(HttpClient http) : IMatchSetupService
{
    public async Task<MatchSetupBootstrapDto> GetBootstrapAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MatchSetupBootstrapDto>(
            "api/match-setup/bootstrap", ct)
        ?? new MatchSetupBootstrapDto(null, [], [], [], []);

    public async Task<List<WizardCourtDto>> GetCourtsByClubAsync(int clubId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<WizardCourtDto>>(
            $"api/match-setup/clubs/{clubId}/courts", ct) ?? [];

    public async Task AutoBookmarkPlayersAsync(
        AutoBookmarkPlayersRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            "api/match-setup/auto-bookmark", request, ct);
        resp.EnsureSuccessStatusCode();
    }
}
