using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// REST surface for <see cref="ICourtClaimService"/>. Phase 7: backs MAUI's
/// HTTP impl so the live-scoring page (TennisScore migration in PR 7e) and
/// the Match landing page can use the same court-claim coordination on both
/// hosts.
/// </summary>
[ApiController]
[Route("api/court-claim")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class CourtClaimController(
    ICourtClaimService courtClaim,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("saved-match/{savedMatchId:int}/is-stale")]
    public async Task<ActionResult<bool>> IsStale(int savedMatchId, CancellationToken ct)
        => await courtClaim.IsStaleAsync(savedMatchId, ct);

    [HttpPost("saved-match/{savedMatchId:int}/extend-grace")]
    public async Task<IActionResult> ExtendGrace(int savedMatchId, CancellationToken ct)
    {
        await courtClaim.ExtendGraceAsync(savedMatchId, ct);
        return NoContent();
    }

    [HttpPost("saved-match/{savedMatchId:int}/end")]
    public async Task<IActionResult> EndMatch(int savedMatchId, CancellationToken ct)
    {
        await courtClaim.EndMatchAsync(savedMatchId, ct);
        return NoContent();
    }

    [HttpGet("active")]
    public async Task<ActionResult<ActiveMatchDto>> GetUserActiveMatch(
        [FromQuery] int? excludingSavedMatchId, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return NoContent();
        var dto = await courtClaim.GetUserActiveMatchAsync(userId, excludingSavedMatchId, ct);
        return dto is null ? NoContent() : dto;
    }

    [HttpGet("courts/{courtId:int}/evaluate")]
    public async Task<ActionResult<CourtClaimResult>> EvaluateCourt(
        int courtId, CancellationToken ct)
        => await courtClaim.EvaluateAsync(courtId, ct);
}
