using DropShot.Maui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace DropShot.Maui;

public static class MauiProgram
{
    /// <summary>Base URL of the DropShot API. Update before publishing.</summary>
    public const string ApiBaseUrl = "https://localhost:7001/";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // ── Shared HttpClient ────────────────────────────────────────────────
        // Both AuthService and ApiService share the same scoped HttpClient so
        // the Bearer token set during login is immediately visible to ApiService.
        builder.Services.AddScoped(_ =>
            new HttpClient { BaseAddress = new Uri(ApiBaseUrl) });

        // ── Auth & API services ──────────────────────────────────────────────
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<AuthService>());
        builder.Services.AddScoped<ApiService>();

        // Required for [Authorize] attributes and <AuthorizeView>
        builder.Services.AddAuthorizationCore();

        // ── MudBlazor ────────────────────────────────────────────────────────
        builder.Services.AddMudServices();

        return builder.Build();
    }
}
