using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Admin-tunable site settings. Backs the Admin/SiteSettings page (phase 5).
/// </summary>
public interface ISiteSettingsService
{
    Task<SiteSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task SetContentMaxWidthPxAsync(int px, CancellationToken ct = default);
}
