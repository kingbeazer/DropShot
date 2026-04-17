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
using Microsoft.AspNetCore.WebUtilities;

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
builder.Services.AddScoped<ActiveRoleService>();
builder.Services.AddScoped<AuthenticationStateProvider, ActiveRoleAuthenticationStateProvider>();

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
    .AddDefaultTokenProviders()
    .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>("MagicLogin");

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserState>();
builder.Services.AddScoped<TennisScoreService>();
builder.Services.AddScoped<ClubAuthorizationService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<EmailTemplateService>();
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

// ── Active-role middleware ────────────────────────────────────────────────────
// Reads the ActiveRole cookie and rebuilds HttpContext.User with only that role
// claim, so SSR AuthorizeView, [Authorize(Roles=...)], and all downstream checks
// see the active role instead of all granted roles.
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true)
    {
        var activeRole = context.Request.Cookies["ActiveRole"];
        var allRoles = user.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).ToList();

        if (!string.IsNullOrEmpty(activeRole) && allRoles.Count > 1 &&
            allRoles.Contains(activeRole, StringComparer.OrdinalIgnoreCase))
        {
            var filteredClaims = user.Claims
                .Where(c => c.Type != System.Security.Claims.ClaimTypes.Role)
                .Append(new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Role, activeRole))
                .ToList();

            var identity = new System.Security.Claims.ClaimsIdentity(
                filteredClaims,
                user.Identity.AuthenticationType,
                System.Security.Claims.ClaimsIdentity.DefaultNameClaimType,
                System.Security.Claims.ClaimTypes.Role);

            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
    }

    await next();
});

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

// ── Magic link login callback (validates token, sets identity cookie, redirects) ──
app.MapGet("/Account/LoginMagicLinkCallback", async (
    string userId,
    string code,
    string? returnUrl,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var user = await userManager.FindByIdAsync(userId);
    if (user is null)
        return Results.Redirect("/Account/Login");

    var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
    var isValid = await userManager.VerifyUserTokenAsync(user, "MagicLogin", "magic-link", decodedCode);
    if (!isValid)
        return Results.Redirect("/Account/Login");

    await signInManager.SignInAsync(user, isPersistent: false);

    // Prevent open redirects
    if (!string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        return Results.LocalRedirect(returnUrl);

    return Results.Redirect("/");
});

// ── Role switching (SSR form POST, mirrors the logout pattern) ───────────────
app.MapPost("/Account/SwitchRole", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<MyDbContext> dbFactory,
    ILogger<Program> logger) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var role = form["role"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/";

    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null) return Results.Redirect("/Account/Login");

    var grantedRoles = await userManager.GetRolesAsync(user);
    if (!grantedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        return Results.Redirect(returnUrl);

    var previousRole = httpContext.Request.Cookies["ActiveRole"]
                       ?? grantedRoles.FirstOrDefault() ?? "";

    // Persist the active role in an HttpOnly cookie
    httpContext.Response.Cookies.Append("ActiveRole", role, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddDays(30)
    });

    // Audit log
    var ip = httpContext.Connection.RemoteIpAddress?.ToString();
    await using var db = dbFactory.CreateDbContext();
    db.RoleSwitchLogs.Add(new RoleSwitchLog
    {
        UserId = user.Id,
        FromRole = previousRole,
        ToRole = role,
        Timestamp = DateTime.UtcNow,
        IpAddress = ip
    });
    await db.SaveChangesAsync();

    logger.LogInformation("Role switch: User {UserId} from {FromRole} to {ToRole} (IP: {Ip})",
        user.Id, previousRole, role, ip);

    return Results.Redirect(returnUrl);
}).RequireAuthorization();

// ── Club switching for ClubAdmin role (multi-club admins) ────────────────────
app.MapPost("/Account/SwitchClub", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<MyDbContext> dbFactory) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var clubIdStr = form["clubId"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/";

    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null) return Results.Redirect("/Account/Login");

    if (!int.TryParse(clubIdStr, out var clubId))
        return Results.Redirect(returnUrl);

    await using var db = dbFactory.CreateDbContext();
    var isAdmin = await db.ClubAdministrators.AnyAsync(ca => ca.UserId == user.Id && ca.ClubId == clubId);
    if (!isAdmin) return Results.Redirect(returnUrl);

    httpContext.Response.Cookies.Append("ActiveClubId", clubId.ToString(), new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddDays(30)
    });

    return Results.Redirect(returnUrl);
}).RequireAuthorization();

// ── Avatar upload (SSR form POST) ──────────────────────────────────────────
app.MapPost("/Account/Manage/UploadAvatar", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment env) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null) return Results.Redirect("/Account/Login");

    var form = await httpContext.Request.ReadFormAsync();
    var file = form.Files.GetFile("avatarFile");
    if (file is null || file.Length == 0)
        return Results.Redirect("/Account/Manage");

    var allowedExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(ext))
        return Results.Redirect("/Account/Manage?error=Invalid+file+type");

    const long maxSize = 2 * 1024 * 1024;
    if (file.Length > maxSize)
        return Results.Redirect("/Account/Manage?error=Image+must+be+less+than+2+MB");

    var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "avatars");
    Directory.CreateDirectory(uploadsDir);

    foreach (var existing in Directory.GetFiles(uploadsDir, $"{user.Id}.*"))
        File.Delete(existing);

    var fileName = $"{user.Id}{ext}";
    var filePath = Path.Combine(uploadsDir, fileName);
    await using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);

    user.ProfileImagePath = $"/uploads/avatars/{fileName}";
    await userManager.UpdateAsync(user);

    return Results.Redirect("/Account/Manage");
}).RequireAuthorization()
  .DisableAntiforgery();

// ── Seed roles ────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in new[] { "SuperAdmin", "Admin", "ClubAdmin", "User" })
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));

    // Ensure every existing user has the base "User" role
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var usersWithoutRole = (await userManager.GetUsersInRoleAsync("User")).Select(u => u.Id).ToHashSet();
    var allUsers = userManager.Users.ToList();
    foreach (var user in allUsers.Where(u => !usersWithoutRole.Contains(u.Id)))
        await userManager.AddToRoleAsync(user, "User");
}

app.Run();
