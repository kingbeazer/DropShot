using System.Text;
using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    JwtTokenService jwtTokenService,
    IDbContextFactory<MyDbContext> dbFactory,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var activeRole = roles.FirstOrDefault() ?? "";
        var token = jwtTokenService.GenerateTokenWithActiveRole(user, roles, activeRole);

        await using var db = dbFactory.CreateDbContext();
        var adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == user.Id)
            .Select(ca => ca.ClubId)
            .ToListAsync();

        return Ok(new LoginResponse(token, user.UserName!, user.Email!, roles, adminClubIds, activeRole, roles));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public async Task<ActionResult<UserInfoDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var grantedRoles = (await userManager.GetRolesAsync(user)).ToList();

        // Read active role from JWT claim, fall back to first granted role
        var activeRole = User.FindFirst("active_role")?.Value
                         ?? grantedRoles.FirstOrDefault() ?? "";

        await using var db = dbFactory.CreateDbContext();
        var adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == user.Id)
            .Select(ca => ca.ClubId)
            .ToListAsync();

        return Ok(new UserInfoDto(user.Id, user.UserName!, user.Email!, grantedRoles, adminClubIds, activeRole, grantedRoles));
    }

    [HttpPost("switch-role")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public async Task<ActionResult<SwitchRoleResponse>> SwitchRole([FromBody] SwitchRoleRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var grantedRoles = (await userManager.GetRolesAsync(user)).ToList();

        // Validate the requested role is in the user's granted roles
        if (!grantedRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = $"Role '{request.Role}' is not in your granted roles." });

        var previousRole = User.FindFirst("active_role")?.Value
                           ?? grantedRoles.FirstOrDefault() ?? "";

        // Log the role switch
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await using var db = dbFactory.CreateDbContext();
        db.RoleSwitchLogs.Add(new RoleSwitchLog
        {
            UserId = user.Id,
            FromRole = previousRole,
            ToRole = request.Role,
            Timestamp = DateTime.UtcNow,
            IpAddress = ip
        });
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Role switch: User {UserId} switched from {FromRole} to {ToRole} (IP: {Ip})",
            user.Id, previousRole, request.Role, ip);

        // Issue a new token with the requested active role
        var token = jwtTokenService.GenerateTokenWithActiveRole(user, grantedRoles, request.Role);

        return Ok(new SwitchRoleResponse(token, request.Role, grantedRoles));
    }

    [HttpPost("magic-link/request")]
    public async Task<IActionResult> RequestMagicLink(
        [FromBody] MagicLinkRequest request,
        [FromServices] EmailService emailService,
        [FromServices] EmailTemplateService emailTemplateService,
        [FromServices] IConfiguration config)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !(await userManager.IsEmailConfirmedAsync(user)))
        {
            // Don't reveal that the user does not exist
            return Ok(new { message = "If that email exists, a sign-in link has been sent." });
        }

        var code = await userManager.GenerateUserTokenAsync(user, "MagicLogin", "magic-link");
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "";
        var callbackUrl = $"{baseUrl}/Account/LoginMagicLinkCallback?userId={user.Id}&code={code}";

        await emailService.SendEmailAsync(request.Email, "Sign In to DropShot",
            emailTemplateService.MagicLinkEmail(callbackUrl), isHtml: true);

        return Ok(new { message = "If that email exists, a sign-in link has been sent." });
    }

    [HttpPost("magic-link/verify")]
    public async Task<ActionResult<LoginResponse>> VerifyMagicLink([FromBody] MagicLinkVerifyRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Unauthorized(new { message = "Invalid or expired magic link." });

        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        var isValid = await userManager.VerifyUserTokenAsync(user, "MagicLogin", "magic-link", decodedCode);
        if (!isValid)
            return Unauthorized(new { message = "Invalid or expired magic link." });

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var activeRole = roles.FirstOrDefault() ?? "";
        var token = jwtTokenService.GenerateTokenWithActiveRole(user, roles, activeRole);

        await using var db = dbFactory.CreateDbContext();
        var adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == user.Id)
            .Select(ca => ca.ClubId)
            .ToListAsync();

        return Ok(new LoginResponse(token, user.UserName!, user.Email!, roles, adminClubIds, activeRole, roles));
    }
}
