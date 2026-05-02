using MudBlazor;

namespace DropShot.UI.Theme;

/// <summary>
/// Shared MudBlazor theme for both the web (DropShot) and MAUI (DropShot.Maui)
/// hosts. Defined once here so the two app shells can't drift — MAUI was
/// shipping with the stock purple palette while the web had moved to teal.
/// </summary>
public static class DropShotTheme
{
    public static MudTheme Default { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary        = "#00897b",
            PrimaryDarken  = "#00695c",
            PrimaryLighten = "#4db6ac",
            Secondary      = "#26a69a",
        }
    };
}
