using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="ISiteSettingsService"/>. Thin adapter over
/// the existing in-process <see cref="SiteSettingsService"/> (which holds the
/// AppSettings cache + EF read/write).
/// </summary>
public sealed class WebSiteSettingsService(SiteSettingsService inner) : ISiteSettingsService
{
    public async Task<SiteSettingsDto> GetSettingsAsync(CancellationToken ct = default) =>
        new(await inner.GetContentMaxWidthPxAsync(ct));

    public Task SetContentMaxWidthPxAsync(int px, CancellationToken ct = default) =>
        inner.SetContentMaxWidthPxAsync(px, ct);
}
