using DropShot.Components;
using DropShot.Components.Account;
using DropShot.Data;
using DropShot.Hubs;
using DropShot.Models;
using DropShot.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor Server ────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

// ── CORS (allow MAUI app to call the API) ────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["https://localhost:7000", "https://localhost:5001"];
builder.Services.AddCors(options =>
    options.AddPolicy("MauiPolicy", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<MyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<MyDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<MyDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── Identity ─────────────────────────────────────────────────────────────────
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// JWT bearer — used by the MAUI app (separate AddAuthentication call)
var jwtCfg = builder.Configuration.GetSection("Jwt");
var authBuilder = builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtCfg["Issuer"],
            ValidAudience = jwtCfg["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtCfg["Key"]!))
        };
    });

// ── External OAuth providers (only registered when credentials are configured) ──
var authCfg = builder.Configuration.GetSection("Authentication");

if (!string.IsNullOrEmpty(authCfg["Google:ClientId"]))
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = authCfg["Google:ClientId"]!;
        options.ClientSecret = authCfg["Google:ClientSecret"]!;
    });

if (!string.IsNullOrEmpty(authCfg["Facebook:AppId"]))
    authBuilder.AddFacebook(options =>
    {
        options.AppId = authCfg["Facebook:AppId"]!;
        options.AppSecret = authCfg["Facebook:AppSecret"]!;
    });

if (!string.IsNullOrEmpty(authCfg["Twitter:ConsumerKey"]))
    authBuilder.AddTwitter(options =>
    {
        options.ConsumerKey = authCfg["Twitter:ConsumerKey"]!;
        options.ConsumerSecret = authCfg["Twitter:ConsumerSecret"]!;
        options.RetrieveUserDetails = true;
    });

if (!string.IsNullOrEmpty(authCfg["Microsoft:ClientId"]))
    authBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = authCfg["Microsoft:ClientId"]!;
        options.ClientSecret = authCfg["Microsoft:ClientSecret"]!;
    });

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MyDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<UserState>();
builder.Services.AddScoped<TennisScoreService>();
builder.Services.AddScoped<ClubAuthorizationService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddScoped<ResultVerificationService>();
builder.Services.AddScoped<AdminEmailService>();
builder.Services.AddScoped<FuzzySearchService>();
builder.Services.AddSingleton<QrLoginService>();
builder.Services.AddHostedService<QrSessionCleanupService>();
builder.Services.AddMudServices();

var app = builder.Build();

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.UseMigrationsEndPoint();
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("MauiPolicy");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();
app.MapHub<ChatHub>("/chathub");
app.MapHub<QrAuthHub>("/qrauthub");

// ── QR code login callback (sets identity cookie, redirects desktop) ─────────
app.MapGet("/Account/QrLogin", async (
    string token,
    QrLoginService qrLoginService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var session = qrLoginService.GetSession(token);
    if (session is null || session.Status != QrSessionStatus.Authenticated || string.IsNullOrEmpty(session.UserId))
        return Results.Redirect("/Account/Login");

    var user = await userManager.FindByIdAsync(session.UserId);
    if (user is null)
        return Results.Redirect("/Account/Login");

    await signInManager.SignInAsync(user, isPersistent: false);
    qrLoginService.RemoveSession(token);

    if (session.CourtId.HasValue)
        return Results.Redirect($"/score?courtId={session.CourtId.Value}");

    return Results.Redirect("/");
});

// ── Seed roles ────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in new[] { "SuperAdmin", "Admin", "ClubAdmin" })
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
}

app.Run();
