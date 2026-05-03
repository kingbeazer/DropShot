using DropShot.Maui.Services;
using DropShot.Shared;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace DropShot.Maui;

public static class MauiProgram
{
    /// <summary>Base URL of the DropShot API. Debug points to local dev server, Release points to production.</summary>
#if DEBUG
    public const string ApiBaseUrl = "https://localhost:7243/";
#else
    public const string ApiBaseUrl = "https://ds.tennis/";
#endif

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

        // Expose ApiBaseUrl as IConfiguration["App:BaseUrl"] so any code that
        // reads that key (HttpScoreboardHubFactory, Upgrade.razor, etc.) gets
        // the right absolute URL on MAUI. Without this, services that build
        // SignalR hub URLs from $"{baseUrl}/chathub" produce relative URIs
        // and SignalR throws System.UriFormatException.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = ApiBaseUrl,
        });

        // ── Shared HttpClient ────────────────────────────────────────────────
        // Both AuthService and ApiService share the same scoped HttpClient so
        // the Bearer token set during login is immediately visible to ApiService.
        builder.Services.AddScoped(_ =>
            new HttpClient { BaseAddress = new Uri(ApiBaseUrl) });

        // ── Auth & API services ──────────────────────────────────────────────
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<AuthService>());
        builder.Services.AddScoped<ICurrentUser, MauiCurrentUser>();
        builder.Services.AddScoped<IEmailService, HttpEmailService>();
        builder.Services.AddScoped<ApiService>();

        // ── DropShot.UI service abstractions (phase 3) ───────────────────
        builder.Services.AddScoped<IPlayerService, HttpPlayerService>();
        builder.Services.AddScoped<IClubService, HttpClubService>();
        builder.Services.AddScoped<IEventService, HttpEventService>();
        builder.Services.AddScoped<ICompetitionService, HttpCompetitionService>();
        builder.Services.AddScoped<ICompetitionAdminService, HttpCompetitionAdminService>();
        builder.Services.AddScoped<IRulesSetService, HttpRulesSetService>();
        builder.Services.AddScoped<ISiteSettingsService, HttpSiteSettingsService>();
        builder.Services.AddScoped<IInvitationService, HttpInvitationService>();
        builder.Services.AddScoped<IMatchService, HttpMatchService>();
        builder.Services.AddScoped<IMatchScoringService, HttpMatchScoringService>();
        builder.Services.AddScoped<IMatchSetupService, HttpMatchSetupService>();
        builder.Services.AddScoped<ICourtClaimService, HttpCourtClaimService>();
        builder.Services.AddScoped<FuzzySearchService>();
        builder.Services.AddScoped<TennisScoreService>();
        builder.Services.AddScoped<UserState>();
        builder.Services.AddScoped<IScoreboardService, HttpScoreboardService>();
        builder.Services.AddScoped<IUserService, HttpUserService>();
        builder.Services.AddScoped<IScoreboardHubFactory, HttpScoreboardHubFactory>();
        builder.Services.AddScoped<IPaymentService, HttpPaymentService>();

        // Required for [Authorize] attributes and <AuthorizeView>
        builder.Services.AddAuthorizationCore();

        // ── MudBlazor ────────────────────────────────────────────────────────
        builder.Services.AddMudServices();

        return builder.Build();
    }
}
