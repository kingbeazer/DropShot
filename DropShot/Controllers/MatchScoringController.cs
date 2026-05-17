using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        await TrySurfaceBearerIdentityAsync();

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

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("ladder-fixture")]
    public async Task<ActionResult<CreateLadderFixtureResponse>> CreateLadderFixture(
        [FromBody] CreateLadderFixtureRequest req, CancellationToken ct)
    {
        try
        {
            var id = await scoring.CreateLadderFixtureAsync(req, ct);
            return new CreateLadderFixtureResponse(id);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [AllowAnonymous]
    [HttpDelete("live-match/{savedMatchId:int}")]
    public async Task<IActionResult> DiscardLiveMatch(
        int savedMatchId, [FromQuery] string? deviceToken, CancellationToken ct)
    {
        await TrySurfaceBearerIdentityAsync();

        try
        {
            await scoring.DiscardLiveMatchAsync(savedMatchId, deviceToken, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// The anonymous scoring endpoints stay <c>[AllowAnonymous]</c> so a
    /// logged-out casual user can score on a device-token. But MAUI / iOS
    /// clients send a JWT bearer when logged in, and on an
    /// <c>[AllowAnonymous]</c> action the Bearer scheme is not invoked
    /// automatically — leaving <c>HttpContext.User</c> unauthenticated and
    /// <c>WebCurrentUser.UserId</c> null, so the SavedMatch ownership
    /// check in <see cref="WebMatchScoringService"/> falls into the
    /// device-token branch and 403s (the authenticated client doesn't
    /// send a DeviceToken). Run the Bearer handler opportunistically:
    /// when a token is present we surface the identity; when it isn't,
    /// the request stays anonymous and the device-token branch handles
    /// it as before.
    /// </summary>
    private async Task TrySurfaceBearerIdentityAsync()
    {
        if (HttpContext.User?.Identity?.IsAuthenticated == true) return;
        var auth = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (auth.Succeeded && auth.Principal is not null)
            HttpContext.User = auth.Principal;
    }
}
