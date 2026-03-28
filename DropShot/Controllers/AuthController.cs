using DropShot.Data;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    JwtTokenService jwtTokenService,
    IDbContextFactory<MyDbContext> dbFactory) : ControllerBase
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
        var token = await jwtTokenService.GenerateTokenAsync(user);

        await using var db = dbFactory.CreateDbContext();
        var adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == user.Id)
            .Select(ca => ca.ClubId)
            .ToListAsync();

        return Ok(new LoginResponse(token, user.UserName!, user.Email!, roles, adminClubIds));
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "Bearer")]
    public async Task<ActionResult<UserInfoDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = (await userManager.GetRolesAsync(user)).ToList();

        await using var db = dbFactory.CreateDbContext();
        var adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == user.Id)
            .Select(ca => ca.ClubId)
            .ToListAsync();

        return Ok(new UserInfoDto(user.Id, user.UserName!, user.Email!, roles, adminClubIds));
    }
}
