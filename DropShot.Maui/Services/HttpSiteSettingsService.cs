using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="ISiteSettingsService"/>. Hits the
/// new <c>GET/PUT /api/site-settings</c> endpoints (phase 5).
/// </summary>
public sealed class HttpSiteSettingsService(HttpClient http) : ISiteSettingsService
{
    public async Task<SiteSettingsDto> GetSettingsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<SiteSettingsDto>("api/site-settings", ct)
            ?? new SiteSettingsDto(SiteSettingsDto.ContentMaxWidthPxDefault);

    public async Task SetContentMaxWidthPxAsync(int px, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            "api/site-settings", new UpdateContentMaxWidthRequest(px), ct);
        resp.EnsureSuccessStatusCode();
    }
}
