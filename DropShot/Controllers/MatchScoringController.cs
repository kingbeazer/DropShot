using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// REST surface for <see cref="IMatchScoringService"/>. Phase 7 prep PR for
/// the TennisScore.razor migration. Anonymous routes back the casual-scoring
/// flow (which TennisScore supports without a login); authenticated routes
/// gate state mutations that require a player profile (preferences, friend
/// requests, fixture finalisation).
/// </summary>
[ApiController]
[Route("api/match-scoring")]
public class MatchScoringController(IMatchScoringService scoring) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("bootstrap")]
    public Task<TennisScoreBootstrapDto> GetBootstrap(CancellationToken ct)
        => scoring.GetBootstrapAsync(ct);

    [AllowAnonymous]
    [HttpGet("saved-match/{savedMatchId:int}/resume")]
    public async Task<ActionResult<SavedMatchResumeDto>> GetSavedMatchForResume(
        int savedMatchId, CancellationToken ct)
    {
        var dto = await scoring.GetSavedMatchForResumeAsync(savedMatchId, ct);
        return dto is null ? NotFound() : dto;
    }

    [AllowAnonymous]
    [HttpGet("fixtures/{fixtureId:int}/scoring-context")]
    public async Task<ActionResult<TennisScoreFixtureContextDto>> GetFixtureContext(
        int fixtureId, CancellationToken ct)
    {
        var dto = await scoring.GetFixtureContextAsync(fixtureId, ct);
        return dto is null ? NotFound() : dto;
    }

    [AllowAnonymous]
    [HttpGet("rubbers/{rubberId:int}/scoring-context")]
    public async Task<ActionResult<TennisScoreRubberContextDto>> GetRubberContext(
        int rubberId, CancellationToken ct)
    {
        var dto = await scoring.GetRubberContextAsync(rubberId, ct);
        return dto is null ? NotFound() : dto;
    }

    [AllowAnonymous]
    [HttpGet("courts/available")]
    public Task<List<ScoringCourtDto>> GetAvailableCourts(
        [FromQuery] int? selectedCourtId, CancellationToken ct)
        => scoring.GetAvailableCourtsAsync(selectedCourtId, ct);

    public sealed record SavePreferredGameScoringRequest(bool GameScoring);

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPut("preferences/game-scoring")]
    public async Task<IActionResult> SavePreferredGameScoring(
        [FromBody] SavePreferredGameScoringRequest req, CancellationToken ct)
    {
        await scoring.SavePreferredGameScoringAsync(req.GameScoring, ct);
        return NoContent();
    }

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("friends/{targetPlayerId:int}/request")]
    public async Task<IActionResult> SendFriendRequest(int targetPlayerId, CancellationToken ct)
    {
        await scoring.SendFriendRequestAsync(targetPlayerId, ct);
        return NoContent();
    }

    public sealed record UpsertLiveMatchResponse(int SavedMatchId);

    [AllowAnonymous]
    [HttpPost("live-match")]
    public async Task<ActionResult<UpsertLiveMatchResponse>> UpsertLiveMatch(
        [FromBody] UpsertLiveMatchRequest req, CancellationToken ct)
    {
        try
        {
            var id = await scoring.UpsertLiveMatchAsync(req, ct);
            return new UpsertLiveMatchResponse(id);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("fixtures/{fixtureId:int}/finalise-live")]
    public async Task<IActionResult> FinaliseLiveFixture(
        int fixtureId, [FromBody] FinaliseLiveFixtureRequest req, CancellationToken ct)
    {
        await scoring.FinaliseLiveFixtureAsync(fixtureId, req, ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpDelete("live-match/{savedMatchId:int}")]
    public async Task<IActionResult> DiscardLiveMatch(
        int savedMatchId, [FromQuery] string? deviceToken, CancellationToken ct)
    {
        try
        {
            await scoring.DiscardLiveMatchAsync(savedMatchId, deviceToken, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
