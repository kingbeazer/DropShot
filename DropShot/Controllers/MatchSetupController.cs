using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// REST surface for <see cref="IMatchSetupService"/>. Phase 7 PR 7e.
/// Bootstrap + courts are anonymous-safe (the wizard is reachable from the
/// anonymous TennisScore route); auto-bookmark requires a current user.
/// </summary>
[ApiController]
[Route("api/match-setup")]
public class MatchSetupController(IMatchSetupService setup) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("bootstrap")]
    public Task<MatchSetupBootstrapDto> GetBootstrap(CancellationToken ct)
        => setup.GetBootstrapAsync(ct);

    [AllowAnonymous]
    [HttpGet("clubs/{clubId:int}/courts")]
    public Task<List<WizardCourtDto>> GetCourtsByClub(int clubId, CancellationToken ct)
        => setup.GetCourtsByClubAsync(clubId, ct);

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("auto-bookmark")]
    public async Task<IActionResult> AutoBookmark(
        [FromBody] AutoBookmarkPlayersRequest req, CancellationToken ct)
    {
        await setup.AutoBookmarkPlayersAsync(req, ct);
        return NoContent();
    }
}
