using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// Admin/edit endpoints for the CompetitionPage. Sibling of
/// <see cref="CompetitionsController"/>; that one is the player-facing
/// read+scoring API, this one wraps server-only operations behind
/// <see cref="ICompetitionAdminService"/>.
/// </summary>
[ApiController]
[Route("api/competitions/admin")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class CompetitionsAdminController(
    ICompetitionAdminService admin,
    ILogger<CompetitionsAdminController> logger) : ControllerBase
{
    // ── Read ─────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/edit")]
    public async Task<ActionResult<CompetitionEditDto>> GetForEdit(int id, CancellationToken ct)
    {
        var dto = await admin.GetCompetitionForEditAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("new/edit")]
    public async Task<ActionResult<CompetitionEditDto>> GetForCreate(CancellationToken ct)
    {
        var dto = await admin.GetCompetitionForEditAsync(null, ct);
        return dto is null ? Forbid() : Ok(dto);
    }

    [HttpGet("seed-source-candidates")]
    public async Task<ActionResult<List<CompetitionSeedSourceDto>>> GetSeedCandidates(
        [FromQuery] int? excludeCompetitionId,
        [FromQuery] CompetitionFormat format,
        [FromQuery] int? hostClubId,
        CancellationToken ct)
        => await admin.GetSeedSourceCandidatesAsync(excludeCompetitionId, format, hostClubId, ct);

    [HttpGet("clubs/{clubId:int}/scheduling-templates")]
    public async Task<ActionResult<List<ClubSchedulingTemplateDto>>> GetClubTemplates(int clubId, CancellationToken ct)
        => await admin.GetClubTemplatesAsync(clubId, ct);

    [HttpGet("clubs/{clubId:int}/email-templates")]
    public async Task<ActionResult<List<ClubEmailTemplateDto>>> GetEmailTemplates(int clubId, CancellationToken ct)
        => await admin.GetEmailTemplatesAsync(clubId, ct);

    [HttpGet("can-edit")]
    public async Task<ActionResult<bool>> CanEdit([FromQuery] int? competitionId, CancellationToken ct)
        => await admin.CanEditCompetitionAsync(competitionId, ct);

    [HttpGet("is-super-admin")]
    public async Task<ActionResult<bool>> IsSuperAdmin(CancellationToken ct)
        => await admin.IsSuperAdminAsync(ct);

    [HttpGet("{id:int}/admins")]
    public async Task<ActionResult<List<CompetitionAdminRowDto>>> GetAdmins(int id, CancellationToken ct)
        => await admin.GetCompetitionAdminsAsync(id, ct);

    // ── Competition lifecycle ────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] SaveCompetitionEditRequest req, CancellationToken ct)
    {
        try { return await admin.SaveCompetitionAsync(null, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<int>> Update(
        int id, [FromBody] SaveCompetitionEditRequest req, CancellationToken ct)
    {
        try { return await admin.SaveCompetitionAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/clone")]
    public async Task<ActionResult<CloneCompetitionResultDto>> Clone(
        int id, [FromBody] CloneCompetitionRequest req, CancellationToken ct)
    {
        try { return await admin.CloneCompetitionAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/toggle-started")]
    public async Task<ActionResult<bool>> ToggleStarted(int id, CancellationToken ct)
    {
        try { return await admin.ToggleStartedAsync(id, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Stages ───────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/stages/follow-up")]
    public async Task<IActionResult> ApplyStageFollowUp(
        int id, [FromBody] ApplyStageFollowUpRequest req, CancellationToken ct)
    {
        try { await admin.ApplyStageFollowUpAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Admins ───────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/admins")]
    public async Task<IActionResult> AddAdmin(
        int id, [FromBody] AddCompetitionAdminRequest req, CancellationToken ct)
    {
        try { await admin.AddCompetitionAdminAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/admins/{userId}")]
    public async Task<IActionResult> RemoveAdmin(int id, string userId, CancellationToken ct)
    {
        try { await admin.RemoveCompetitionAdminAsync(id, userId, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Rubber template ──────────────────────────────────────────────────────

    [HttpGet("{id:int}/rubber-template")]
    public async Task<ActionResult<RubberTemplateStateDto>> GetRubberTemplate(int id, CancellationToken ct)
    {
        try { return await admin.LoadRubberTemplateStateAsync(id, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/rubber-template/preset")]
    public async Task<IActionResult> ApplyPreset(
        int id, [FromBody] ApplyRubberPresetRequest req, CancellationToken ct)
    {
        try { await admin.ApplyRubberPresetAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/rubber-template/convert-to-custom")]
    public async Task<IActionResult> ConvertToCustom(int id, CancellationToken ct)
    {
        try { await admin.ConvertToCustomTemplateAsync(id, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/rubber-template/rows")]
    public async Task<IActionResult> AddCustomRow(int id, CancellationToken ct)
    {
        try { await admin.AddCustomRubberRowAsync(id, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/rubber-template/rows")]
    public async Task<IActionResult> SaveCustomRow(
        int id, [FromBody] SaveRubberRowRequest req, CancellationToken ct)
    {
        try { await admin.SaveRubberRowAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}/rubber-template/rows/{order:int}")]
    public async Task<IActionResult> DeleteCustomRow(int id, int order, CancellationToken ct)
    {
        try { await admin.DeleteCustomRubberRowAsync(id, order, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/rubber-template/revert")]
    public async Task<IActionResult> RevertToDefault(int id, CancellationToken ct)
    {
        try { await admin.RevertToDefaultTemplateAsync(id, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Email ────────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/email/match")]
    public async Task<IActionResult> SendMatchEmail(
        int id, [FromBody] SendMatchEmailRequest req, CancellationToken ct)
    {
        try { await admin.SendMatchEmailAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/email/competition")]
    public async Task<IActionResult> SendCompetitionEmail(
        int id, [FromBody] SendCompetitionEmailRequest req, CancellationToken ct)
    {
        try { await admin.SendCompetitionEmailAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Participants ─────────────────────────────────────────────────────────

    [HttpPost("{id:int}/participants/search")]
    public async Task<ActionResult<List<PlayerSearchResultDto>>> SearchPlayers(
        int id, [FromBody] SearchPlayersForAddRequest req, CancellationToken ct)
    {
        try { return await admin.SearchPlayersAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/participants")]
    public async Task<IActionResult> AddParticipant(
        int id, [FromBody] AddParticipantRequest req, CancellationToken ct)
    {
        try { await admin.AddParticipantAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/participants/{playerId:int}")]
    public async Task<IActionResult> RemoveParticipant(int id, int playerId, CancellationToken ct)
    {
        try { await admin.RemoveParticipantAsync(id, playerId, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:int}/participants/{playerId:int}/status")]
    public async Task<IActionResult> UpdateParticipantStatus(
        int id, int playerId, [FromBody] UpdateParticipantStatusRequest req, CancellationToken ct)
    {
        try { await admin.UpdateParticipantStatusAsync(id, playerId, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:int}/participants/{playerId:int}/team")]
    public async Task<IActionResult> AssignParticipantTeam(
        int id, int playerId, [FromBody] AssignParticipantTeamRequest req, CancellationToken ct)
    {
        try { await admin.AssignParticipantTeamAsync(id, playerId, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:int}/participants/{playerId:int}/role")]
    public async Task<IActionResult> AssignParticipantRole(
        int id, int playerId, [FromBody] SetParticipantRoleRequest req, CancellationToken ct)
    {
        try { await admin.AssignParticipantRoleAsync(id, playerId, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:int}/participants/{playerId:int}/division")]
    public async Task<IActionResult> AssignParticipantDivision(
        int id, int playerId, [FromBody] SetParticipantDivisionRequest req, CancellationToken ct)
    {
        try { await admin.AssignParticipantDivisionAsync(id, playerId, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/light-players")]
    public async Task<ActionResult<int>> CreateLightPlayer(
        int id, [FromBody] CreateLightPlayerForCompetitionRequest req, CancellationToken ct)
    {
        try { return await admin.CreateLightPlayerAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/light-players/{playerId:int}")]
    public async Task<IActionResult> SaveLightPlayer(
        int id, int playerId, [FromBody] SaveLightPlayerRequest req, CancellationToken ct)
    {
        try { await admin.SaveLightPlayerAsync(id, playerId, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Divisions ────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/divisions")]
    public async Task<ActionResult<int>> CreateDivision(
        int id, [FromBody] SaveDivisionRequest req, CancellationToken ct)
    {
        try { return await admin.SaveDivisionAsync(id, null, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/divisions/{divisionId:int}")]
    public async Task<ActionResult<int>> UpdateDivision(
        int id, int divisionId, [FromBody] SaveDivisionRequest req, CancellationToken ct)
    {
        try { return await admin.SaveDivisionAsync(id, divisionId, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/divisions/{divisionId:int}")]
    public async Task<IActionResult> DeleteDivision(int id, int divisionId, CancellationToken ct)
    {
        try { await admin.DeleteDivisionAsync(id, divisionId, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/divisions/seed")]
    public async Task<IActionResult> SeedDivisions(
        int id, [FromBody] SeedDivisionsFromPreviousRequest req, CancellationToken ct)
    {
        try { await admin.RunSeedDivisionsAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/teams/{teamId:int}/division")]
    public async Task<IActionResult> AssignTeamDivision(
        int id, int teamId, [FromBody] AssignTeamDivisionRequest req, CancellationToken ct)
    {
        try { await admin.AssignTeamDivisionAsync(id, teamId, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/teams")]
    public async Task<ActionResult<int>> CreateTeam(
        int id, [FromBody] SaveTeamRequest req, CancellationToken ct)
    {
        try { return await admin.SaveTeamAsync(id, null, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/teams/{teamId:int}")]
    public async Task<ActionResult<int>> UpdateTeam(
        int id, int teamId, [FromBody] SaveTeamRequest req, CancellationToken ct)
    {
        try { return await admin.SaveTeamAsync(id, teamId, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/teams/{teamId:int}")]
    public async Task<IActionResult> DeleteTeam(int id, int teamId, CancellationToken ct)
    {
        try { await admin.DeleteTeamAsync(id, teamId, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}/teams")]
    public async Task<IActionResult> DeleteAllTeams(int id, CancellationToken ct)
    {
        try { await admin.DeleteAllTeamsAsync(id, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/teams/captain")]
    public async Task<IActionResult> AssignCaptain(
        int id, [FromBody] AssignCaptainRequest req, CancellationToken ct)
    {
        try { await admin.AssignCaptainAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/teams/auto-captain")]
    public async Task<ActionResult<int>> AutoAssignCaptains(int id, CancellationToken ct)
    {
        try { return await admin.AutoAssignCaptainsAsync(id, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/teams/generate-preview")]
    public async Task<ActionResult<GenerateTeamsResultDto>> GenerateTeamsPreview(
        int id, [FromBody] GenerateTeamsRequest req, CancellationToken ct)
    {
        try { return await admin.GenerateTeamsPreviewAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/teams/generate-confirm")]
    public async Task<IActionResult> ConfirmGenerateTeams(
        int id, [FromBody] ConfirmGenerateTeamsRequest req, CancellationToken ct)
    {
        try { await admin.ConfirmGenerateTeamsAsync(id, req, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/teams/{teamId:int}/validate")]
    public async Task<ActionResult<ValidateTeamResultDto>> ValidateTeam(int id, int teamId, CancellationToken ct)
    {
        try { return await admin.ValidateTeamAsync(id, teamId, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/fixtures")]
    public async Task<ActionResult<int>> CreateFixture(
        int id, [FromBody] SaveFixtureRequest req, CancellationToken ct)
    {
        try { return await admin.SaveFixtureAsync(id, null, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/fixtures/{fixtureId:int}")]
    public async Task<ActionResult<int>> UpdateFixture(
        int id, int fixtureId, [FromBody] SaveFixtureRequest req, CancellationToken ct)
    {
        try { return await admin.SaveFixtureAsync(id, fixtureId, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/fixtures/{fixtureId:int}")]
    public async Task<IActionResult> DeleteFixture(int id, int fixtureId, CancellationToken ct)
    {
        try { await admin.DeleteFixtureAsync(id, fixtureId, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/fixtures")]
    public async Task<IActionResult> DeleteAllFixtures(int id, CancellationToken ct)
    {
        try { await admin.DeleteAllFixturesAsync(id, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/fixtures/{fixtureId:int}/dialog")]
    public async Task<ActionResult<CompetitionFixtureDto>> LoadFixtureForDialog(int id, int fixtureId, CancellationToken ct)
    {
        try
        {
            var dto = await admin.LoadFixtureForDialogAsync(id, fixtureId, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id:int}/fixtures/confirm-assignment")]
    public async Task<ActionResult<ConfirmFixtureAssignmentResultDto>> ConfirmFixtureAssignment(
        int id, [FromBody] ConfirmFixtureAssignmentRequest req, CancellationToken ct)
    {
        try { return await admin.ConfirmFixtureAssignmentAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/fixtures/schedule")]
    public async Task<ActionResult<ScheduleFixturesResultDto>> ScheduleFixtures(
        int id, [FromBody] ScheduleFixturesAdminRequest req, CancellationToken ct)
    {
        try { return await admin.ScheduleFixturesAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/fixtures/simulate-rr")]
    public async Task<ActionResult<SimulateRoundRobinResultDto>> SimulateRoundRobin(int id, CancellationToken ct)
    {
        try { return await admin.SimulateRoundRobinAsync(id, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/fixtures/seed-knockout")]
    public async Task<ActionResult<SeedKnockoutFromStandingsResultDto>> SeedKnockoutFromStandings(int id, CancellationToken ct)
    {
        try { return await admin.SeedKnockoutFromStandingsAsync(id, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Match windows ────────────────────────────────────────────────────────

    [HttpPost("{id:int}/match-windows")]
    public async Task<ActionResult<int>> AddMatchWindow(
        int id, [FromBody] SaveMatchWindowRequest req, CancellationToken ct)
    {
        try { return await admin.AddMatchWindowAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/match-windows/{matchWindowId:int}")]
    public async Task<IActionResult> DeleteMatchWindow(int id, int matchWindowId, CancellationToken ct)
    {
        try { await admin.DeleteMatchWindowAsync(id, matchWindowId, ct); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/divisions/{divisionId:int}/match-windows")]
    public async Task<ActionResult<int>> AddDivisionMatchWindow(
        int id, int divisionId, [FromBody] SaveMatchWindowRequest req, CancellationToken ct)
    {
        try { return await admin.AddDivisionMatchWindowAsync(id, divisionId, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/match-windows/import-from-template")]
    public async Task<ActionResult<int>> ImportMatchWindowsFromTemplate(
        int id, [FromBody] ImportMatchWindowsFromTemplateRequest req, CancellationToken ct)
    {
        try { return await admin.ImportMatchWindowsFromTemplateAsync(id, req, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
