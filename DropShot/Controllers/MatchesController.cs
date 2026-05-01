using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// Match-scoring endpoints. Phase 5 covers the landing page read; scoring
/// writes land in phase 6.
/// </summary>
[ApiController]
[Route("api/matches")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class MatchesController(IMatchService matches) : ControllerBase
{
    /// <summary>
    /// Active (incomplete) matches the caller can see. Authenticated users
    /// always get their own UserId-scoped matches; anonymous device-token
    /// matches aren't relevant on MAUI (the app requires login), so the route
    /// is JWT-gated.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<ActiveMatchDto>>> GetMine(
        [FromQuery] string? deviceToken, CancellationToken ct)
    {
        return await matches.GetMyActiveMatchesAsync(deviceToken, ct);
    }
}
