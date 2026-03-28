using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class CompetitionsController(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CompetitionDto>>> GetAll()
    {
        await using var db = dbFactory.CreateDbContext();
        var comps = await db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .OrderBy(c => c.CompetitionName)
            .ToListAsync();
        return comps.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompetitionDetailDto>> Get(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var c = await db.Competition
            .Include(x => x.HostClub)
            .Include(x => x.Rules)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder))
            .Include(x => x.Participants).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(x => x.CompetitionID == id);

        if (c is null) return NotFound();

        return new CompetitionDetailDto(
            c.CompetitionID, c.CompetitionName,
            (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
            c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge,
            (DropShot.Shared.PlayerSex?)c.EligibleSex,
            c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
            c.Stages.Select(s => new CompetitionStageDto(
                s.CompetitionStageId, s.Name, s.StageOrder,
                (DropShot.Shared.StageType)s.StageType)).ToList(),
            c.Participants.Select(p => new CompetitionParticipantDto(
                p.PlayerId, p.Player?.DisplayName ?? "",
                (DropShot.Shared.ParticipantStatus)p.Status,
                p.RegisteredAt)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CompetitionDto>> Create([FromBody] SaveCompetitionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = new Competition();
        Apply(comp, req);
        db.Competition.Add(comp);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = comp.CompetitionID }, ToDto(comp));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CompetitionDto>> Update(int id, [FromBody] SaveCompetitionRequest req)
    {
        if (!await authzService.CanEditCompetitionAsync(User, req.HostClubId))
            return Forbid();

        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();

        Apply(comp, req);
        await db.SaveChangesAsync();
        return ToDto(comp);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        db.Competition.Remove(comp);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Stages ────────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/stages")]
    public async Task<IActionResult> AddStage(int id, [FromBody] AddStageRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var stage = new CompetitionStage
        {
            CompetitionId = id, Name = req.Name, StageOrder = req.StageOrder,
            StageType = (DropShot.Models.StageType)req.StageType
        };
        db.CompetitionStages.Add(stage);
        await db.SaveChangesAsync();
        return Ok(new CompetitionStageDto(stage.CompetitionStageId, stage.Name,
            stage.StageOrder, (DropShot.Shared.StageType)stage.StageType));
    }

    [HttpDelete("{id:int}/stages/{stageId:int}")]
    public async Task<IActionResult> DeleteStage(int id, int stageId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var stage = await db.CompetitionStages.FindAsync(stageId);
        if (stage is null || stage.CompetitionId != id) return NotFound();
        db.CompetitionStages.Remove(stage);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Participants ──────────────────────────────────────────────────────────

    [HttpPost("{id:int}/participants")]
    public async Task<IActionResult> AddParticipant(int id, [FromBody] AddParticipantRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = new CompetitionParticipant
        {
            CompetitionId = id, PlayerId = req.PlayerId,
            RegisteredAt = DateTime.UtcNow, Status = DropShot.Models.ParticipantStatus.Registered
        };
        db.CompetitionParticipants.Add(cp);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id:int}/participants/{playerId:int}")]
    public async Task<IActionResult> UpdateParticipantStatus(
        int id, int playerId, [FromBody] UpdateParticipantStatusRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        cp.Status = (DropShot.Models.ParticipantStatus)req.Status;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}/participants/{playerId:int}")]
    public async Task<IActionResult> RemoveParticipant(int id, int playerId)
    {
        await using var db = dbFactory.CreateDbContext();
        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        db.CompetitionParticipants.Remove(cp);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Competition c, SaveCompetitionRequest r)
    {
        c.CompetitionName = r.CompetitionName.Trim();
        c.CompetitionFormat = (DropShot.Models.CompetitionFormat)r.CompetitionFormat;
        c.MaxParticipants = r.MaxParticipants;
        c.StartDate = r.StartDate;
        c.EndDate = r.EndDate;
        c.MaxAge = r.MaxAge;
        c.EligibleSex = (DropShot.Models.PlayerSex?)r.EligibleSex;
        c.HostClubId = r.HostClubId;
        c.RulesSetId = r.RulesSetId;
    }

    private static CompetitionDto ToDto(Competition c) => new(
        c.CompetitionID, c.CompetitionName,
        (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge,
        (DropShot.Shared.PlayerSex?)c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name);
}
