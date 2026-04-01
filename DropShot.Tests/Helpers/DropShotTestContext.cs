using Bunit;
using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
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
        contextAccessor.HttpContext.Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());
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

        var resultVerification = Substitute.For<ResultVerificationService>(
            emailService, config, NullLogger<ResultVerificationService>.Instance);
        Services.AddScoped(_ => resultVerification);

        var adminEmailSvc = Substitute.For<AdminEmailService>(
            emailService, config, NullLogger<AdminEmailService>.Instance);
        Services.AddScoped(_ => adminEmailSvc);

        Services.AddScoped(_ => Substitute.For<JwtTokenService>(config, userManager));

        // Logging
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    /// <summary>
    /// Returns a DbContext for seeding test data before rendering.
    /// </summary>
    public MyDbContext SeedDatabase() => DbFactory.CreateDbContext();
}
