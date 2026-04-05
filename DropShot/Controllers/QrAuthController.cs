using DropShot.Data;
using DropShot.Hubs;
using DropShot.Models;
using DropShot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DropShot.Controllers;

[ApiController]
[Route("api/auth/qr")]
public class QrAuthController(
    QrLoginService qrLoginService,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IHubContext<QrAuthHub> hubContext) : ControllerBase
{
    [HttpPost("generate")]
    public ActionResult Generate()
    {
        var session = qrLoginService.GenerateSession();
        return Ok(new { session.Token });
    }

    [HttpGet("status")]
    public ActionResult GetStatus([FromQuery] string session)
    {
        var qrSession = qrLoginService.GetSession(session);
        if (qrSession is null)
            return NotFound(new { status = "not_found" });

        return Ok(new
        {
            status = qrSession.Status.ToString().ToLowerInvariant(),
            userId = qrSession.UserId,
            userName = qrSession.UserName,
            roles = qrSession.Roles,
            courtId = qrSession.CourtId
        });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] QrConfirmRequest request)
    {
        var session = qrLoginService.GetSession(request.Token);
        if (session is null)
            return NotFound(new { message = "Session not found." });

        if (session.Status == QrSessionStatus.Expired || session.IsExpired)
            return BadRequest(new { message = "Session has expired." });

        if (session.Status != QrSessionStatus.Pending)
            return BadRequest(new { message = "Session already used." });

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = (await userManager.GetRolesAsync(user)).ToList();

        var confirmed = qrLoginService.ConfirmSession(
            request.Token, user.Id, user.UserName!, roles, request.CourtId);

        if (!confirmed)
            return BadRequest(new { message = "Failed to confirm session." });

        await hubContext.Clients.Group($"qr-{request.Token}")
            .SendAsync("QrAuthConfirmed", new
            {
                userId = user.Id,
                userName = user.UserName,
                roles,
                courtId = request.CourtId
            });

        return Ok(new { message = "Login confirmed." });
    }
}

public record QrConfirmRequest(string Token, string Email, string Password, int? CourtId);
