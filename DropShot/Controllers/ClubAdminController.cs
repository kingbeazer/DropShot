using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// Cross-club moderation endpoints for the ClubAdmin/Admin/SuperAdmin roles.
/// Backs ClubAdmin/ClubLinkRequests (phase 5). All routes require an admin
/// role; per-request authorisation (does the caller administer the request's
/// club?) lives in <see cref="IClubService"/>.
/// </summary>
[ApiController]
[Route("api/clubadmin")]
[Authorize(AuthenticationSchemes = "Bearer", Roles = "ClubAdmin,Admin,SuperAdmin")]
public class ClubAdminController(IClubService clubs) : ControllerBase
{
    [HttpGet("link-requests")]
    public async Task<ActionResult<List<ClubLinkRequestDto>>> GetPendingLinkRequests(CancellationToken ct)
    {
        return await clubs.GetPendingLinkRequestsForAdminAsync(ct);
    }

    [HttpPost("link-requests/{requestId:int}/approve")]
    public async Task<IActionResult> Approve(int requestId, CancellationToken ct)
    {
        try
        {
            await clubs.ApproveLinkRequestAsync(requestId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("link-requests/{requestId:int}/reject")]
    public async Task<IActionResult> Reject(int requestId, CancellationToken ct)
    {
        try
        {
            await clubs.RejectLinkRequestAsync(requestId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
