using Microsoft.Maui.Storage;

namespace DropShot.Maui.Services;

/// <summary>
/// Light/dark mode state for the MAUI host. Persists across launches via
/// Preferences so the user's choice survives app restarts. Mirrors the web
/// app's localStorage-backed toggle (see DropShot/wwwroot/js/theme-switcher.js).
/// </summary>
public sealed class ThemeState
{
    private const string PreferenceKey = "dropshot-dark-mode";
    private bool _isDarkMode;

    public ThemeState()
    {
        _isDarkMode = Preferences.Default.Get(PreferenceKey, true);
    }

    public bool IsDarkMode => _isDarkMode;

    public event Action? Changed;

    public void Toggle()
    {
        _isDarkMode = !_isDarkMode;
        Preferences.Default.Set(PreferenceKey, _isDarkMode);
        Changed?.Invoke();
    }
}
