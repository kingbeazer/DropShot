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
}
