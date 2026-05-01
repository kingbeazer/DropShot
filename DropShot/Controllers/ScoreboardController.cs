using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// Live scoreboard read/write endpoints. Backs Scoreboard.razor on MAUI
/// (phase 6). Live updates flow through the existing /chathub SignalR hub
/// (separate from these REST endpoints).
/// </summary>
[ApiController]
[Route("api/scoreboard")]
[Authorize(AuthenticationSchemes = "Bearer", Roles = "ClubAdmin,Admin,SuperAdmin")]
public class ScoreboardController(IScoreboardService scoreboard) : ControllerBase
{
    [HttpGet("courts")]
    public async Task<ActionResult<List<ScoreboardCourtDto>>> GetCourts(CancellationToken ct)
    {
        return await scoreboard.GetAdminCourtsAsync(ct);
    }

    [HttpGet("courts/{courtId:int}/state")]
    public async Task<ActionResult<ScoreboardCourtStateDto>> GetCourtState(int courtId, CancellationToken ct)
    {
        return await scoreboard.GetCourtStateAsync(courtId, ct);
    }

    [HttpPut("courts/{courtId:int}/display-setting")]
    public async Task<IActionResult> UpdateDisplaySetting(
        int courtId, [FromBody] UpdateDisplaySettingRequest req, CancellationToken ct)
    {
        await scoreboard.UpdateDisplaySettingAsync(courtId, req, ct);
        return NoContent();
    }
}
