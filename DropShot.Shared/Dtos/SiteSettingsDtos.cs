namespace DropShot.Shared.Dtos;

/// <summary>
/// Admin-tunable, app-wide settings. Currently exposes only the body content
/// max-width (top navbar + footer always span the full viewport).
/// </summary>
public record SiteSettingsDto(int ContentMaxWidthPx)
{
    public const int ContentMaxWidthPxDefault = 1280;
    public const int ContentMaxWidthPxMin = 960;
    public const int ContentMaxWidthPxMax = 2400;
}

public record UpdateContentMaxWidthRequest(int ContentMaxWidthPx);
