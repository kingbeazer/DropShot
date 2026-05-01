using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// Light-player invitation endpoints. Backs the My Players invite flow
/// (phase 5). The Invite + MobileAuth + VerifyResult acceptance flows extend
/// this in later phases.
/// </summary>
[ApiController]
[Route("api/invitations")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class InvitationsController(IInvitationService invitations) : ControllerBase
{
    [HttpPost("light-player/{lightPlayerId:int}")]
    public async Task<ActionResult<LightPlayerInvitationDto>> CreateOrReuse(
        int lightPlayerId, CancellationToken ct)
    {
        try
        {
            return await invitations.CreateOrReuseLightPlayerInvitationAsync(lightPlayerId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Forbid();
        }
    }

    [HttpPost("{token:guid}/send-email")]
    public async Task<IActionResult> SendEmail(
        Guid token, [FromBody] SendInvitationEmailRequest req, CancellationToken ct)
    {
        try
        {
            await invitations.SendInvitationEmailAsync(token, req.Email, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
