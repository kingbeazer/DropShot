using Bunit;
using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSubstitute;

namespace DropShot.Tests.Helpers;

public class DropShotTestContext : BunitContext
{
    public TestDbContextFactory DbFactory { get; }
    public TestAuthStateProvider AuthProvider { get; }

    public DropShotTestContext(
        bool authenticated = true,
        string userId = "test-user-id",
        string userName = "test@example.com",
        string[]? roles = null)
    {
        // Allow all JS interop calls (MudBlazor uses JS heavily)
        JSInterop.Mode = JSRuntimeMode.Loose;

        DbFactory = new TestDbContextFactory();
        AuthProvider = new TestAuthStateProvider(authenticated, userId, userName, roles);

        // Database
        Services.AddSingleton<IDbContextFactory<MyDbContext>>(DbFactory);
        Services.AddScoped(_ => DbFactory.CreateDbContext());

        // Auth — use bUnit's built-in authorization support
        var authCtx = this.AddAuthorization();
        if (authenticated)
        {
            authCtx.SetAuthorized(userName);
            authCtx.SetClaims(
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, userName));
            if (roles != null)
                authCtx.SetRoles(roles);
        }

        // Also register the custom AuthenticationStateProvider for pages that inject it directly
        Services.AddSingleton<AuthenticationStateProvider>(AuthProvider);

        // MudBlazor
        Services.AddMudServices();

        // Configuration
        var config = Substitute.For<IConfiguration>();
        config["App:BaseUrl"].Returns("https://localhost");
        config["Jwt:Key"].Returns("test-key-that-is-long-enough-for-hmac-sha256-signing!");
        config["Jwt:Issuer"].Returns("test-issuer");
        config["Jwt:Audience"].Returns("test-audience");
        config["ACS:ConnectionString"].Returns("endpoint=https://fake.communication.azure.com/;accesskey=fake");
        config["ACS:SenderAddress"].Returns("noreply@test.com");
        Services.AddSingleton(config);

        // Identity mocks
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore,
            null!, null!, null!, null!, null!, null!, null!, null!);
        Services.AddSingleton(userManager);

        var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        if (authenticated)
        {
            // Mirror the bUnit AuthorizationContext claims onto the HttpContext so
            // services that read the user via IHttpContextAccessor (e.g.
            // WebCompetitionAdminService) see the same identity as components
            // that inject AuthenticationStateProvider.
            var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
                new(System.Security.Claims.ClaimTypes.Email, userName),
                new(System.Security.Claims.ClaimTypes.Name, userName),
            };
            if (roles != null)
            {
                foreach (var role in roles)
                    claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
            }
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
        contextAccessor.HttpContext.Returns(httpContext);
        Services.AddSingleton(contextAccessor);

        var signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager, contextAccessor, Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);
        Services.AddSingleton(signInManager);

        // App services
        Services.AddScoped(_ => new UserState());
        Services.AddScoped(_ => Substitute.For<TennisScoreService>());

        var clubAuthz = Substitute.For<ClubAuthorizationService>(userManager, DbFactory);
        Services.AddScoped(_ => clubAuthz);

        var emailService = Substitute.For<EmailService>(config);
        Services.AddSingleton(emailService);

        var emailTemplateService = new EmailTemplateService(config);
        Services.AddSingleton(emailTemplateService);

        var resultVerification = Substitute.For<ResultVerificationService>(
            emailService, emailTemplateService, config, NullLogger<ResultVerificationService>.Instance);
        Services.AddScoped(_ => resultVerification);

        var adminEmailSvc = Substitute.For<AdminEmailService>(
            emailService, emailTemplateService, config, NullLogger<AdminEmailService>.Instance);
        Services.AddScoped(_ => adminEmailSvc);

        Services.AddScoped(_ => Substitute.For<JwtTokenService>(config, userManager));

        var courtClaim = new CourtClaimService(DbFactory);
        Services.AddScoped(_ => courtClaim);
        Services.AddScoped<ICourtClaimService>(_ => courtClaim);

        Services.AddSingleton<QrLoginService>();
        Services.AddSingleton<BackgroundTaskQueue>();
        Services.AddScoped<ICompetitionRubberTemplateProvider, CompetitionRubberTemplateProvider>();
        Services.AddScoped<RubberResolutionService>();
        Services.AddScoped<FixtureSimulationService>();
        Services.AddScoped<CompetitionSchedulerService>();
        Services.AddScoped<FuzzySearchService>();

        // DropShot.UI auth + service abstractions — register Web impls so
        // bUnit can render any RCL page that injects these.
        Services.AddScoped<ICurrentUser, WebCurrentUser>();
        Services.AddScoped<IPlayerService, WebPlayerService>();
        Services.AddScoped<IClubService, WebClubService>();
        Services.AddScoped<IEventService, WebEventService>();
        Services.AddScoped<ICompetitionService, WebCompetitionService>();
        Services.AddScoped<ICompetitionAdminService, WebCompetitionAdminService>();
        Services.AddScoped<IRulesSetService, WebRulesSetService>();
        Services.AddScoped<ISiteSettingsService, WebSiteSettingsService>();
        Services.AddScoped<IInvitationService, WebInvitationService>();
        Services.AddScoped<IMatchService, WebMatchService>();
        Services.AddScoped<IMatchScoringService, WebMatchScoringService>();
        Services.AddScoped<IMatchSetupService, WebMatchSetupService>();
        Services.AddScoped<IScoreboardService, WebScoreboardService>();
        Services.AddScoped<IUserService, WebUserService>();
        Services.AddScoped<IScoreboardHubFactory, WebScoreboardHubFactory>();
        Services.AddScoped<IPaymentService, WebPaymentService>();

        // Logging
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    /// <summary>
    /// Returns a DbContext for seeding test data before rendering.
    /// </summary>
    public MyDbContext SeedDatabase() => DbFactory.CreateDbContext();
}
