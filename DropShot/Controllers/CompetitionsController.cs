using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class CompetitionsController(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    UserManager<ApplicationUser> userManager,
    ICompetitionRubberTemplateProvider rubberTemplateProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CompetitionDto>>> GetAll(
        [FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] int? eventId = null,
        [FromQuery] bool includeArchived = false)
    {
        await using var db = dbFactory.CreateDbContext();
        var baseQuery = db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .Include(c => c.Event)
            .AsQueryable();

        if (!includeArchived)
            baseQuery = baseQuery.Where(c => !c.IsArchived);

        if (eventId.HasValue)
            baseQuery = baseQuery.Where(c => c.EventId == eventId.Value);

        // Hard visibility filter — caller only sees competitions they can enter.
        var ctx = await authzService.GetVisibilityContextAsync(User);
        var query = authzService.ApplyVisibilityFilter(baseQuery, db, ctx);

        var comps = await query
            .OrderBy(c => c.CompetitionName)
            .Skip(skip)
            .Take(Math.Min(take, 200))
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
            .Include(x => x.Event)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder))
            .Include(x => x.Participants).ThenInclude(p => p.Player)
            .Include(x => x.Participants).ThenInclude(p => p.Team)
            .Include(x => x.Participants).ThenInclude(p => p.Division)
            .Include(x => x.Divisions)
            .Include(x => x.AllowedPlayers)
            .FirstOrDefaultAsync(x => x.CompetitionID == id);

        if (c is null) return NotFound();

        // Enforce hard visibility for the detail view too.
        if (!await authzService.CanViewCompetitionAsync(User, id))
            return NotFound();

        return new CompetitionDetailDto(
            c.CompetitionID, c.CompetitionName,
            (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
            c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
            (DropShot.Shared.PlayerSex?)c.EligibleSex,
            c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
            c.EventId, c.Event?.Name,
            c.Stages.Select(s => new CompetitionStageDto(
                s.CompetitionStageId, s.Name, s.StageOrder,
                (DropShot.Shared.StageType)s.StageType)).ToList(),
            c.Participants.Select(p => new CompetitionParticipantDto(
                p.PlayerId, p.Player?.DisplayName ?? "",
                (DropShot.Shared.ParticipantStatus)p.Status,
                p.RegisteredAt, p.TeamId, p.Team?.Name,
                p.Player?.MobileNumber,
                p.Role,
                (DropShot.Shared.PlayerSex?)p.Player?.Sex,
                p.CompetitionDivisionId,
                p.Division?.Name)).ToList(),
            c.IsArchived,
            c.IsStarted,
            c.CreatorUserId,
            c.IsRestricted,
            c.AllowedPlayers.Select(ap => ap.PlayerId).ToList(),
            c.HasDivisions,
            c.Divisions.OrderBy(d => d.Rank)
                .Select(d => new CompetitionDivisionDto(d.CompetitionDivisionId, d.CompetitionId, d.Rank, d.Name))
                .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionDto>> Create([FromBody] SaveCompetitionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = new Competition();

        if (req.HostClubId.HasValue)
        {
            if (!await authzService.CanCreateClubCompetitionAsync(User, req.HostClubId.Value))
                return Forbid();
            comp.HostClubId = req.HostClubId.Value;
            comp.CreatorUserId = null;
        }
        else
        {
            var appUser = await userManager.GetUserAsync(User);
            if (!authzService.CanCreateUserCompetition(User, appUser))
                return Forbid();
            comp.HostClubId = null;
            comp.CreatorUserId = userManager.GetUserId(User);
        }

        Apply(comp, req);
        ApplyRestriction(comp, req);

        if (comp.HostClubId.HasValue && comp.CreatorUserId != null)
            return BadRequest(new { message = "A competition cannot have both a host club and a creator." });

        db.Competition.Add(comp);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = comp.CompetitionID }, ToDto(comp));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CompetitionDto>> Update(int id, [FromBody] SaveCompetitionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition
            .Include(c => c.AllowedPlayers)
            .FirstOrDefaultAsync(c => c.CompetitionID == id);
        if (comp is null) return NotFound();

        // Authorization uses the stored HostClubId (immutable across updates) rather than the
        // request payload, so callers can't change the owner by tweaking the body.
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId, comp.CompetitionID))
            return Forbid();

        // Creator/host ownership is immutable after creation.
        if (req.HostClubId != comp.HostClubId)
            return BadRequest(new { message = "The host club of a competition cannot be changed." });

        Apply(comp, req);
        UpdateRestriction(db, comp, req);
        await db.SaveChangesAsync();
        return ToDto(comp);
    }

    [HttpPut("{id:int}/archive")]
    public async Task<IActionResult> ToggleArchive(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId, comp.CompetitionID))
            return Forbid();

        comp.IsArchived = !comp.IsArchived;
        await db.SaveChangesAsync();
        return Ok(new { comp.IsArchived });
    }

    [HttpPut("{id:int}/start")]
    public async Task<IActionResult> ToggleStarted(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId, comp.CompetitionID))
            return Forbid();

        comp.IsStarted = !comp.IsStarted;
        await db.SaveChangesAsync();
        return Ok(new { comp.IsStarted });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId, comp.CompetitionID))
            return Forbid();
        if (!comp.IsArchived)
            return BadRequest("Competition must be archived before it can be deleted.");

        // Remove child entities that use Restrict/NoAction FK to avoid FK violations
        var rubbers = await db.Rubbers
            .Where(r => r.Fixture.CompetitionId == id)
            .ToListAsync();
        db.Rubbers.RemoveRange(rubbers);

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id)
            .ToListAsync();
        db.CompetitionFixtures.RemoveRange(fixtures);

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

        var modelType = (DropShot.Models.StageType)req.StageType;
        var nextOrder = await db.CompetitionStages
            .Where(s => s.CompetitionId == id)
            .Select(s => (int?)s.StageOrder).MaxAsync() ?? 0;
        var stage = new CompetitionStage
        {
            CompetitionId = id,
            Name = req.Name ?? StageDisplayName(modelType),
            StageOrder = req.StageOrder ?? nextOrder + 1,
            StageType = modelType
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

        var alreadyRegistered = await db.CompetitionParticipants
            .AnyAsync(cp => cp.CompetitionId == id && cp.PlayerId == req.PlayerId);
        if (alreadyRegistered)
            return Conflict(new { message = "Player is already registered in this competition." });

        if (comp.MaxParticipants.HasValue)
        {
            var currentCount = await db.CompetitionParticipants
                .CountAsync(p => p.CompetitionId == id);
            if (currentCount >= comp.MaxParticipants.Value)
                return BadRequest(new { message = "Competition has reached its maximum number of participants." });
        }

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
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

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
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        db.CompetitionParticipants.Remove(cp);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/participants/{playerId:int}/team")]
    public async Task<IActionResult> AssignParticipantTeam(
        int id, int playerId, [FromBody] AssignParticipantTeamRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        cp.TeamId = req.TeamId;
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── Teams ─────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/teams")]
    public async Task<ActionResult<List<CompetitionTeamDto>>> GetTeams(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var teams = await db.CompetitionTeams
            .Where(t => t.CompetitionId == id)
            .Include(t => t.Captain)
            .OrderBy(t => t.Name)
            .ToListAsync();
        return teams.Select(t => new CompetitionTeamDto(
            t.CompetitionTeamId, t.CompetitionId, t.Name,
            t.CaptainPlayerId, t.Captain?.DisplayName)).ToList();
    }

    [HttpPost("{id:int}/teams")]
    public async Task<IActionResult> AddTeam(int id, [FromBody] SaveTeamRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var team = new CompetitionTeam { CompetitionId = id, Name = req.Name.Trim() };
        db.CompetitionTeams.Add(team);
        await db.SaveChangesAsync();
        return Ok(new CompetitionTeamDto(team.CompetitionTeamId, team.CompetitionId, team.Name));
    }

    [HttpPut("{id:int}/teams/{teamId:int}")]
    public async Task<IActionResult> UpdateTeam(int id, int teamId, [FromBody] SaveTeamRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var team = await db.CompetitionTeams.FindAsync(teamId);
        if (team is null || team.CompetitionId != id) return NotFound();
        team.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}/teams/{teamId:int}")]
    public async Task<IActionResult> DeleteTeam(int id, int teamId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var team = await db.CompetitionTeams.FindAsync(teamId);
        if (team is null || team.CompetitionId != id) return NotFound();

        // Unassign participants from this team before deleting
        var members = await db.CompetitionParticipants
            .Where(cp => cp.TeamId == teamId)
            .ToListAsync();
        foreach (var m in members) m.TeamId = null;

        db.CompetitionTeams.Remove(team);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/fixtures")]
    public async Task<ActionResult<List<CompetitionFixtureDto>>> GetFixtures(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id)
            .Include(f => f.Stage)
            .Include(f => f.Court)
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .Include(f => f.Player3)
            .Include(f => f.Player4)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Include(f => f.CourtPair)
            .OrderBy(f => f.RoundNumber).ThenBy(f => f.ScheduledAt)
            .ToListAsync();
        return fixtures.Select(ToFixtureDto).ToList();
    }

    [HttpPost("{id:int}/fixtures")]
    public async Task<IActionResult> AddFixture(int id, [FromBody] SaveFixtureRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        if (req.CompetitionStageId.HasValue)
        {
            var stage = await db.CompetitionStages.FindAsync(req.CompetitionStageId.Value);
            if (stage is null || stage.CompetitionId != id)
                return BadRequest(new { message = "Stage does not belong to this competition." });
        }

        if (req.CourtId.HasValue)
        {
            var court = await db.Courts.FindAsync(req.CourtId.Value);
            if (court is null || court.ClubId != comp.HostClubId)
                return BadRequest(new { message = "Court does not belong to the competition's host club." });
        }

        var playerIds = new[] { req.Player1Id, req.Player2Id, req.Player3Id, req.Player4Id }
            .Where(id => id.HasValue).Select(id => id!.Value).ToList();
        if (playerIds.Count > 0)
        {
            var existingCount = await db.Players.CountAsync(p => playerIds.Contains(p.PlayerId));
            if (existingCount != playerIds.Count)
                return BadRequest(new { message = "One or more player IDs are invalid." });
        }

        var fixture = new CompetitionFixture { CompetitionId = id };
        ApplyFixture(fixture, req);
        db.CompetitionFixtures.Add(fixture);
        await db.SaveChangesAsync();
        return Ok(fixture.CompetitionFixtureId);
    }

    [HttpPut("{id:int}/fixtures/{fixtureId:int}")]
    public async Task<IActionResult> UpdateFixture(int id, int fixtureId, [FromBody] SaveFixtureRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        if (req.CompetitionStageId.HasValue)
        {
            var stage = await db.CompetitionStages.FindAsync(req.CompetitionStageId.Value);
            if (stage is null || stage.CompetitionId != id)
                return BadRequest(new { message = "Stage does not belong to this competition." });
        }

        if (req.CourtId.HasValue)
        {
            var court = await db.Courts.FindAsync(req.CourtId.Value);
            if (court is null || court.ClubId != comp.HostClubId)
                return BadRequest(new { message = "Court does not belong to the competition's host club." });
        }

        var playerIds = new[] { req.Player1Id, req.Player2Id, req.Player3Id, req.Player4Id }
            .Where(id => id.HasValue).Select(id => id!.Value).ToList();
        if (playerIds.Count > 0)
        {
            var existingCount = await db.Players.CountAsync(p => playerIds.Contains(p.PlayerId));
            if (existingCount != playerIds.Count)
                return BadRequest(new { message = "One or more player IDs are invalid." });
        }

        var fixture = await db.CompetitionFixtures.FindAsync(fixtureId);
        if (fixture is null || fixture.CompetitionId != id) return NotFound();
        ApplyFixture(fixture, req);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}/fixtures/{fixtureId:int}")]
    public async Task<IActionResult> DeleteFixture(int id, int fixtureId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var fixture = await db.CompetitionFixtures.FindAsync(fixtureId);
        if (fixture is null || fixture.CompetitionId != id) return NotFound();
        db.CompetitionFixtures.Remove(fixture);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/fixtures/{fixtureId:int}/result")]
    public async Task<IActionResult> DeleteFixtureResult(int id, int fixtureId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var fixture = await db.CompetitionFixtures
            .Include(f => f.Stage)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId && f.CompetitionId == id);
        if (fixture is null) return NotFound();

        if (fixture.Status != FixtureStatus.Completed && fixture.Status != FixtureStatus.AwaitingVerification)
            return BadRequest(new { message = "Only completed or awaiting-verification fixtures can have their result deleted." });

        var oldWinnerPlayerId = fixture.WinnerPlayerId;
        var oldWinnerTeamId = fixture.WinnerTeamId;
        bool isTeamFixture = fixture.HomeTeamId.HasValue;

        // A fixture is "downstream" of the deleted fixture only if it lives in a
        // later stage (by StageOrder) or — within the same stage — a later round.
        // Peer matches in the same round or other round-robin group matches are
        // NOT downstream, even though the same player may appear in them as a
        // regular participant.
        var currentStageOrder = fixture.Stage?.StageOrder;
        var currentRound = fixture.RoundNumber;

        bool IsDownstream(CompetitionFixture f)
        {
            if (currentStageOrder.HasValue && f.Stage != null
                && f.Stage.StageOrder > currentStageOrder.Value)
                return true;
            if (f.CompetitionStageId == fixture.CompetitionStageId
                && currentRound.HasValue && f.RoundNumber.HasValue
                && f.RoundNumber.Value > currentRound.Value)
                return true;
            return false;
        }

        // Check for downstream fixtures that have already progressed beyond Scheduled
        if (oldWinnerPlayerId.HasValue || oldWinnerTeamId.HasValue)
        {
            var downstreamFixtures = await db.CompetitionFixtures
                .Include(f => f.Stage)
                .Where(f => f.CompetitionId == id
                    && f.CompetitionFixtureId != fixtureId
                    && (f.Status == FixtureStatus.InProgress || f.Status == FixtureStatus.Completed
                        || f.Status == FixtureStatus.AwaitingVerification))
                .ToListAsync();

            bool blocked = isTeamFixture
                ? downstreamFixtures.Any(f => IsDownstream(f)
                    && (f.HomeTeamId == oldWinnerTeamId || f.AwayTeamId == oldWinnerTeamId))
                : downstreamFixtures.Any(f => IsDownstream(f)
                    && (f.Player1Id == oldWinnerPlayerId || f.Player2Id == oldWinnerPlayerId));

            if (blocked)
                return BadRequest(new { message = "Cannot delete this result because the winner has already been placed in a downstream match that is in progress or completed. Delete that result first." });
        }

        // Clear winner from any downstream Scheduled fixtures (next round / next stage only)
        if (oldWinnerPlayerId.HasValue)
        {
            var scheduled = await db.CompetitionFixtures
                .Include(f => f.Stage)
                .Where(f => f.CompetitionId == id
                    && f.CompetitionFixtureId != fixtureId
                    && f.Status == FixtureStatus.Scheduled
                    && (f.Player1Id == oldWinnerPlayerId || f.Player2Id == oldWinnerPlayerId))
                .ToListAsync();

            foreach (var nf in scheduled.Where(IsDownstream))
            {
                if (nf.Player1Id == oldWinnerPlayerId) nf.Player1Id = null;
                if (nf.Player2Id == oldWinnerPlayerId) nf.Player2Id = null;
            }
        }

        if (oldWinnerTeamId.HasValue)
        {
            var scheduled = await db.CompetitionFixtures
                .Include(f => f.Stage)
                .Include(f => f.Rubbers)
                .Where(f => f.CompetitionId == id
                    && f.CompetitionFixtureId != fixtureId
                    && f.Status == FixtureStatus.Scheduled
                    && (f.HomeTeamId == oldWinnerTeamId || f.AwayTeamId == oldWinnerTeamId))
                .ToListAsync();

            foreach (var nf in scheduled.Where(IsDownstream))
            {
                if (nf.HomeTeamId == oldWinnerTeamId) nf.HomeTeamId = null;
                if (nf.AwayTeamId == oldWinnerTeamId) nf.AwayTeamId = null;
                // Remove any resolved Rubbers for the cleared fixture; they'll be recreated on next open
                if (nf.Rubbers.Any())
                    db.Rubbers.RemoveRange(nf.Rubbers);
            }
        }

        // Remove linked SavedMatch if present
        if (fixture.SavedMatchId.HasValue)
        {
            var savedMatch = await db.SavedMatch.FindAsync(fixture.SavedMatchId.Value);
            if (savedMatch != null)
                db.SavedMatch.Remove(savedMatch);
            fixture.SavedMatchId = null;
        }

        // Reset the fixture
        fixture.Status = FixtureStatus.Scheduled;
        fixture.CompletedAt = null;
        fixture.ResultSummary = null;
        fixture.WinnerPlayerId = null;
        fixture.WinnerTeamId = null;
        fixture.VerificationToken = null;
        fixture.OriginalResultSummary = null;
        fixture.OriginalWinnerPlayerId = null;
        fixture.ResultModifiedByAdmin = false;

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/fixtures/schedule")]
    public async Task<IActionResult> ScheduleFixtures(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition
            .Include(c => c.Stages.OrderBy(s => s.StageOrder))
            .Include(c => c.Participants).ThenInclude(p => p.Player)
            .Include(c => c.Teams)
            .Include(c => c.CourtPairs)
            .Include(c => c.MatchWindows).ThenInclude(w => w.Court)
            .FirstOrDefaultAsync(c => c.CompetitionID == id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        // Delete existing fixtures that are not completed, in progress, or awaiting verification
        var existing = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id
                && f.Status != DropShot.Models.FixtureStatus.Completed
                && f.Status != DropShot.Models.FixtureStatus.AwaitingVerification
                && f.Status != DropShot.Models.FixtureStatus.InProgress)
            .ToListAsync();
        db.CompetitionFixtures.RemoveRange(existing);

        var startDate = comp.StartDate?.Date ?? DateTime.Today;
        var endDate = comp.EndDate?.Date ?? DateTime.Today.AddDays(28);
        if (endDate <= startDate) endDate = startDate.AddDays(28);

        var activePlayers = comp.Participants
            .Where(p => p.Status == DropShot.Models.ParticipantStatus.Registered ||
                        p.Status == DropShot.Models.ParticipantStatus.Confirmed)
            .Select(p => p.PlayerId)
            .ToList();

        var rng = new Random();
        IReadOnlyList<DropShot.Models.CompetitionMatchWindow> matchWindows = comp.MatchWindows.ToList();
        var courts = comp.HostClubId.HasValue
            ? await db.Courts.Where(c => c.ClubId == comp.HostClubId.Value).ToListAsync()
            : new List<DropShot.Models.Court>();
        var occupied = new HashSet<(DateTime, int?)>();

        (DateTime, int?) RandomCourtSlot()
        {
            var result = SchedulingSlotPicker.PickCourtSlot(matchWindows, courts, occupied, startDate, endDate, rng);
            occupied.Add(result);
            return result;
        }

        CompetitionFixture NewFixture(int compId, int stageId)
        {
            var (slot, courtId) = RandomCourtSlot();
            return new CompetitionFixture
            {
                CompetitionId = compId,
                CompetitionStageId = stageId,
                ScheduledAt = slot,
                CourtId = courtId,
                Status = DropShot.Models.FixtureStatus.Scheduled
            };
        }

        var isTeamFormat = comp.CompetitionFormat is DropShot.Models.CompetitionFormat.TeamMatch
            or DropShot.Models.CompetitionFormat.Team
            or DropShot.Models.CompetitionFormat.Doubles
            or DropShot.Models.CompetitionFormat.MixedDoubles;

        foreach (var stage in comp.Stages)
        {
            switch (stage.StageType)
            {
                case DropShot.Models.StageType.RoundRobin:
                {
                    if (isTeamFormat && comp.Teams.Count >= 2)
                    {
                        var courtPairIds = comp.CourtPairs.Select(cp => cp.CourtPairId).ToList();
                        int cpIdx = 0;

                        // Load all team members for Doubles/MixedDoubles player assignment
                        var allMembers = comp.Participants
                            .Where(p => p.TeamId != null && p.Player != null
                                && (p.Status == DropShot.Models.ParticipantStatus.Registered
                                    || p.Status == DropShot.Models.ParticipantStatus.Confirmed))
                            .ToList();

                        // When divisions are configured, generate one round-robin per
                        // division so teams only play other teams in their tier. Teams
                        // with no division assignment fall into an "unassigned" group
                        // (and play each other) — useful before divisions are set up.
                        IEnumerable<IGrouping<int?, CompetitionTeam>> teamGroups = comp.HasDivisions
                            ? comp.Teams.GroupBy(t => (int?)t.CompetitionDivisionId)
                            : new[] { comp.Teams.GroupBy(t => (int?)null).First() };

                        foreach (var group in teamGroups)
                        {
                            var teamIds = group.Select(t => t.CompetitionTeamId).ToList();
                            if (teamIds.Count < 2) continue;

                            // Circle method: fix team[0], rotate rest
                            bool hasOdd = teamIds.Count % 2 != 0;
                            if (hasOdd) teamIds.Add(-1); // BYE sentinel
                            int total = teamIds.Count;
                            int rounds = total - 1;

                            for (int round = 0; round < rounds; round++)
                            {
                                for (int match = 0; match < total / 2; match++)
                                {
                                    int home = match == 0 ? 0 : ((round + match - 1) % (total - 1)) + 1;
                                    int away = ((round + total - 1 - match) % (total - 1)) + 1;
                                    if (match == 0) { home = 0; away = ((round) % (total - 1)) + 1; }

                                    int homeTeamId = teamIds[home];
                                    int awayTeamId = teamIds[away];

                                    if (homeTeamId == -1 || awayTeamId == -1) continue; // BYE

                                    var fixture = NewFixture(id, stage.CompetitionStageId);
                                    fixture.HomeTeamId = homeTeamId;
                                    fixture.AwayTeamId = awayTeamId;

                                    if (courtPairIds.Count > 0)
                                    {
                                        fixture.CourtPairId = courtPairIds[cpIdx % courtPairIds.Count];
                                        cpIdx++;
                                    }

                                    // For non-team doubles formats, also set Player1-4 from team members
                                    if (comp.CompetitionFormat is DropShot.Models.CompetitionFormat.Doubles
                                        or DropShot.Models.CompetitionFormat.MixedDoubles)
                                    {
                                        var homePair = allMembers.Where(m => m.TeamId == homeTeamId).ToList();
                                        var awayPair = allMembers.Where(m => m.TeamId == awayTeamId).ToList();
                                        if (homePair.Count >= 1) fixture.Player1Id = homePair[0].PlayerId;
                                        if (homePair.Count >= 2) fixture.Player3Id = homePair[1].PlayerId;
                                        if (awayPair.Count >= 1) fixture.Player2Id = awayPair[0].PlayerId;
                                        if (awayPair.Count >= 2) fixture.Player4Id = awayPair[1].PlayerId;
                                    }

                                    db.CompetitionFixtures.Add(fixture);
                                    // Rubbers are created lazily when the fixture is first opened for scoring.
                                }
                            }
                        }
                    }
                    else
                    {
                        // Individual player round-robin (Singles or formats without teams)
                        var players = activePlayers.ToList();
                        for (int i = 0; i < players.Count; i++)
                        {
                            for (int j = i + 1; j < players.Count; j++)
                            {
                                var fixture = NewFixture(id, stage.CompetitionStageId);
                                fixture.Player1Id = players[i];
                                fixture.Player2Id = players[j];
                                db.CompetitionFixtures.Add(fixture);
                            }
                        }
                    }
                    break;
                }

                case DropShot.Models.StageType.Knockout:
                {
                    int n = (isTeamFormat && comp.Teams.Count >= 2)
                        ? comp.Teams.Count
                        : activePlayers.Count;
                    if (n < 2) break;

                    if (n >= 8)
                    {
                        // ── Quarter-Finals (players assigned automatically when RR completes) ──
                        for (int m = 0; m < 4; m++)
                        {
                            var qf = NewFixture(id, stage.CompetitionStageId);
                            qf.FixtureLabel = $"Quarter-Final {m + 1}";
                            qf.RoundNumber = 1;
                            db.CompetitionFixtures.Add(qf);
                        }

                        // ── Semi-Finals (players assigned automatically when QF completes) ───
                        for (int m = 0; m < 2; m++)
                        {
                            var sf = NewFixture(id, stage.CompetitionStageId);
                            sf.FixtureLabel = $"Semi-Final {m + 1}";
                            sf.RoundNumber = 2;
                            db.CompetitionFixtures.Add(sf);
                        }
                    }
                    else
                    {
                        // ── Semi-Finals (players assigned automatically when RR completes) ────
                        for (int m = 0; m < 2; m++)
                        {
                            var sf = NewFixture(id, stage.CompetitionStageId);
                            sf.FixtureLabel = $"Semi-Final {m + 1}";
                            sf.RoundNumber = 1;
                            db.CompetitionFixtures.Add(sf);
                        }
                    }

                    // ── Final (always a placeholder) ─────────────────────────────
                    var final1 = NewFixture(id, stage.CompetitionStageId);
                    final1.FixtureLabel = "Final";
                    final1.RoundNumber = n >= 8 ? 3 : 2;
                    db.CompetitionFixtures.Add(final1);
                    break;
                }

                case DropShot.Models.StageType.Final:
                {
                    var final2 = NewFixture(id, stage.CompetitionStageId);
                    final2.FixtureLabel = "Final";
                    final2.RoundNumber = 1;
                    db.CompetitionFixtures.Add(final2);
                    break;
                }

                case DropShot.Models.StageType.QuarterFinal:
                {
                    for (int m = 0; m < 4; m++)
                    {
                        var qf = NewFixture(id, stage.CompetitionStageId);
                        qf.FixtureLabel = $"Quarter-Final {m + 1}";
                        qf.RoundNumber = 1;
                        db.CompetitionFixtures.Add(qf);
                    }
                    break;
                }

                case DropShot.Models.StageType.SemiFinal:
                {
                    for (int m = 0; m < 2; m++)
                    {
                        var sf = NewFixture(id, stage.CompetitionStageId);
                        sf.FixtureLabel = $"Semi-Final {m + 1}";
                        sf.RoundNumber = 1;
                        db.CompetitionFixtures.Add(sf);
                    }
                    break;
                }
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    // ── League Table ──────────────────────────────────────────────────────────

    [HttpGet("{id:int}/leaguetable")]
    public async Task<ActionResult<List<LeagueTableEntryDto>>> GetLeagueTable(int id)
    {
        await using var db = dbFactory.CreateDbContext();

        // Find round-robin stage IDs for this competition
        var rrStageIds = await db.CompetitionStages
            .Where(s => s.CompetitionId == id && s.StageType == DropShot.Models.StageType.RoundRobin)
            .Select(s => s.CompetitionStageId)
            .ToListAsync();

        if (rrStageIds.Count == 0) return Ok(new List<LeagueTableEntryDto>());

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id
                        && f.CompetitionStageId != null
                        && rrStageIds.Contains(f.CompetitionStageId!.Value)
                        && f.Status == DropShot.Models.FixtureStatus.Completed
                        && f.WinnerPlayerId != null)
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .ToListAsync();

        // Gather all participant player IDs + names
        var participants = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == id)
            .Include(cp => cp.Player)
            .ToListAsync();

        // Mutable counters
        var played = participants.ToDictionary(cp => cp.PlayerId, _ => 0);
        var won = participants.ToDictionary(cp => cp.PlayerId, _ => 0);
        var lost = participants.ToDictionary(cp => cp.PlayerId, _ => 0);

        foreach (var f in fixtures)
        {
            var ids = new[] { f.Player1Id, f.Player2Id, f.Player3Id, f.Player4Id }
                .Where(pid => pid.HasValue)
                .Select(pid => pid!.Value)
                .Distinct()
                .Where(pid => played.ContainsKey(pid))
                .ToList();

            foreach (var pid in ids)
            {
                played[pid]++;
                if (pid == f.WinnerPlayerId)
                    won[pid]++;
                else if (f.WinnerPlayerId.HasValue)
                    lost[pid]++;
            }
        }

        // Build head-to-head lookup for tiebreaking
        var h2h = new HashSet<(int winner, int loser)>();
        foreach (var f in fixtures)
        {
            var loserIds = new[] { f.Player1Id, f.Player2Id }
                .Where(pid => pid.HasValue && pid.Value != f.WinnerPlayerId!.Value)
                .Select(pid => pid!.Value);
            foreach (var lid in loserIds)
                h2h.Add((f.WinnerPlayerId!.Value, lid));
        }

        var entries = participants
            .Select(cp => new LeagueTableEntryDto(
                cp.PlayerId,
                cp.Player?.DisplayName ?? "",
                played[cp.PlayerId],
                won[cp.PlayerId],
                lost[cp.PlayerId],
                won[cp.PlayerId] * 3))
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.Won)
            .ThenByDescending(e => h2h.Count(h => h.winner == e.PlayerId))
            .ThenBy(e => e.Lost)
            .ToList();

        return entries;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Apply(Competition c, SaveCompetitionRequest r)
    {
        c.CompetitionName = r.CompetitionName.Trim();
        c.CompetitionFormat = (DropShot.Models.CompetitionFormat)r.CompetitionFormat;
        c.MaxParticipants = r.MaxParticipants;
        c.StartDate = r.StartDate;
        c.EndDate = r.EndDate;
        c.MaxAge = r.MaxAge;
        c.MinAge = r.MinAge;
        c.EligibleSex = (DropShot.Models.PlayerSex?)r.EligibleSex;
        // HostClubId/CreatorUserId are set explicitly at Create time and are immutable thereafter.
        c.RulesSetId = r.RulesSetId;
        c.EventId = r.EventId;
        c.HasDivisions = r.HasDivisions;
        c.SeededFromCompetitionId = r.SeededFromCompetitionId;
    }

    // Populates the restriction + allow-list on a fresh Competition (Create path).
    private static void ApplyRestriction(Competition c, SaveCompetitionRequest r)
    {
        c.IsRestricted = r.IsRestricted;
        if (r.IsRestricted && r.AllowedPlayerIds is { Count: > 0 })
        {
            foreach (var pid in r.AllowedPlayerIds.Distinct())
                c.AllowedPlayers.Add(new CompetitionAllowedPlayer { PlayerId = pid });
        }
    }

    // Diffs the restriction state on an existing Competition (Update path).
    private static void UpdateRestriction(MyDbContext db, Competition c, SaveCompetitionRequest r)
    {
        c.IsRestricted = r.IsRestricted;

        var desired = (r.IsRestricted && r.AllowedPlayerIds is { Count: > 0 })
            ? r.AllowedPlayerIds.Distinct().ToHashSet()
            : new HashSet<int>();

        var toRemove = c.AllowedPlayers.Where(ap => !desired.Contains(ap.PlayerId)).ToList();
        foreach (var ap in toRemove) c.AllowedPlayers.Remove(ap);

        var existing = c.AllowedPlayers.Select(ap => ap.PlayerId).ToHashSet();
        foreach (var pid in desired.Where(p => !existing.Contains(p)))
            c.AllowedPlayers.Add(new CompetitionAllowedPlayer { PlayerId = pid });
    }

    private static void ApplyFixture(CompetitionFixture f, SaveFixtureRequest r)
    {
        f.CompetitionStageId = r.CompetitionStageId;
        f.CourtId = r.CourtId;
        f.ScheduledAt = r.ScheduledAt;
        f.FixtureLabel = r.FixtureLabel;
        f.RoundNumber = r.RoundNumber;
        f.Player1Id = r.Player1Id;
        f.Player2Id = r.Player2Id;
        f.Player3Id = r.Player3Id;
        f.Player4Id = r.Player4Id;
        f.Status = (DropShot.Models.FixtureStatus)r.Status;
        if (r.ResultSummary != null) f.ResultSummary = r.ResultSummary;
        if (r.WinnerPlayerId != null) f.WinnerPlayerId = r.WinnerPlayerId;
    }

    private static CompetitionDto ToDto(Competition c) => new(
        c.CompetitionID, c.CompetitionName,
        (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
        (DropShot.Shared.PlayerSex?)c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
        c.EventId, c.Event?.Name, c.IsArchived, c.IsStarted,
        c.CreatorUserId, c.IsRestricted);

    private static CompetitionFixtureDto ToFixtureDto(CompetitionFixture f) => new(
        f.CompetitionFixtureId, f.CompetitionId,
        f.CompetitionStageId, f.Stage?.Name,
        f.CourtId, f.Court?.Name,
        f.ScheduledAt, (DropShot.Shared.FixtureStatus)f.Status,
        f.FixtureLabel, f.RoundNumber,
        f.Player1Id, f.Player1?.DisplayName,
        f.Player2Id, f.Player2?.DisplayName,
        f.Player3Id, f.Player3?.DisplayName,
        f.Player4Id, f.Player4?.DisplayName,
        f.ResultSummary, f.WinnerPlayerId,
        f.HomeTeamId, f.HomeTeam?.Name,
        f.AwayTeamId, f.AwayTeam?.Name,
        f.WinnerTeamId, f.CourtPairId, f.CourtPair?.Name);

    // ── Team Match Endpoints ─────────────────────────────────────────────────

    [HttpPut("{id:int}/participants/{playerId:int}/role")]
    public async Task<IActionResult> SetParticipantRole(
        int id, int playerId, [FromBody] SetParticipantRoleRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        cp.Role = req.Role;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id:int}/teams/{teamId:int}/captain")]
    public async Task<IActionResult> SetTeamCaptain(
        int id, int teamId, [FromBody] SetTeamCaptainRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var team = await db.CompetitionTeams.FindAsync(teamId);
        if (team is null || team.CompetitionId != id) return NotFound();

        var isMember = await db.CompetitionParticipants
            .AnyAsync(cp => cp.CompetitionId == id && cp.PlayerId == req.CaptainPlayerId && cp.TeamId == teamId);
        if (!isMember)
            return BadRequest(new { message = "Captain must be a member of the team." });

        team.CaptainPlayerId = req.CaptainPlayerId;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/teams/{teamId:int}/validate")]
    public async Task<ActionResult<TeamValidationResultDto>> ValidateTeam(int id, int teamId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();

        var members = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == id && cp.TeamId == teamId
                && (cp.Status == DropShot.Models.ParticipantStatus.Registered
                    || cp.Status == DropShot.Models.ParticipantStatus.Confirmed))
            .Include(cp => cp.Player)
            .ToListAsync();

        var errors = new List<string>();
        var requiredRoles = await rubberTemplateProvider.GetRoleSetAsync(db, id);

        if (requiredRoles.Count == 0)
            return new TeamValidationResultDto(true, errors);

        foreach (var role in requiredRoles)
        {
            var count = members.Count(m => m.Role == role);
            if (count == 0) errors.Add($"Missing role '{role}'.");
            else if (count > 1) errors.Add($"Duplicate role '{role}' ({count} players).");
        }

        var unassigned = members.Where(m => string.IsNullOrEmpty(m.Role)).ToList();
        foreach (var m in unassigned)
            errors.Add($"{m.Player?.DisplayName ?? "Unknown"} has no role assigned.");

        return new TeamValidationResultDto(errors.Count == 0, errors);
    }

    // ── Rubber Template ─────────────────────────────────────────────────────

    [HttpGet("rubber-presets")]
    public ActionResult<List<RubberPresetDto>> GetRubberPresets() =>
        RubberTemplateRegistry.AvailablePresets()
            .Select(p => new RubberPresetDto(p.Key, p.Label))
            .ToList();

    [HttpGet("{id:int}/rubber-template")]
    public async Task<ActionResult<RubberTemplateDto>> GetRubberTemplate(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition
            .AsNoTracking()
            .Include(c => c.RubberTemplate)
                .ThenInclude(t => t!.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == id);
        if (comp is null) return NotFound();

        var template = await rubberTemplateProvider.GetAsync(db, id);
        var roles = RubberTemplateRegistry.GetRoleSet(template);

        string source;
        if (comp.RubberTemplate is { Rubbers.Count: > 0 }) source = "custom";
        else if (!string.IsNullOrEmpty(comp.RubberTemplateKey)) source = $"preset:{comp.RubberTemplateKey}";
        else source = "default";

        var defs = (template ?? [])
            .Select(d => new RubberTemplateDefDto(d.Order, d.Name, d.CourtNumber, d.HomeRoles, d.AwayRoles))
            .ToList();

        return new RubberTemplateDto(source, comp.RubberTemplateKey, roles, defs);
    }

    [HttpPut("{id:int}/rubber-template/preset")]
    public async Task<IActionResult> SetRubberTemplateKey(
        int id, [FromBody] SetCompetitionRubberTemplateKeyRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var key = string.IsNullOrWhiteSpace(req.TemplateKey) ? null : req.TemplateKey!.Trim();
        if (key != null && !RubberTemplateRegistry.AvailablePresets().Any(p => p.Key == key))
            return BadRequest(new { message = $"Unknown preset '{key}'." });

        comp.RubberTemplateKey = key;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id:int}/rubber-template")]
    public async Task<IActionResult> SaveCustomRubberTemplate(
        int id, [FromBody] SaveRubberTemplateRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition
            .Include(c => c.RubberTemplate)
                .ThenInclude(t => t!.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        if (req.Rubbers.Count == 0)
            return BadRequest(new { message = "A custom template must contain at least one rubber." });
        if (req.Rubbers.Any(r => string.IsNullOrWhiteSpace(r.Name)))
            return BadRequest(new { message = "Every rubber must have a name." });
        if (req.Rubbers.Any(r => r.HomeRoles.Count == 0 || r.AwayRoles.Count == 0))
            return BadRequest(new { message = "Every rubber must have at least one home and one away role." });

        if (comp.RubberTemplate == null)
        {
            comp.RubberTemplate = new CompetitionRubberTemplate { CompetitionId = id };
            db.CompetitionRubberTemplates.Add(comp.RubberTemplate);
        }
        else
        {
            db.RubberTemplateRubbers.RemoveRange(comp.RubberTemplate.Rubbers);
            comp.RubberTemplate.Rubbers.Clear();
        }

        int order = 1;
        foreach (var def in req.Rubbers.OrderBy(r => r.Order))
        {
            comp.RubberTemplate.Rubbers.Add(new RubberTemplateRubber
            {
                Order = order++,
                Name = def.Name.Trim(),
                CourtNumber = def.CourtNumber < 1 ? 1 : def.CourtNumber,
                HomeRoles = def.HomeRoles.Select(r => r.Trim()).Where(r => r.Length > 0).ToList(),
                AwayRoles = def.AwayRoles.Select(r => r.Trim()).Where(r => r.Length > 0).ToList(),
            });
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}/rubber-template")]
    public async Task<IActionResult> DeleteCustomRubberTemplate(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var template = await db.CompetitionRubberTemplates
            .FirstOrDefaultAsync(t => t.CompetitionId == id);
        if (template != null)
        {
            db.CompetitionRubberTemplates.Remove(template);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Court Pairs ──────────────────────────────────────────────────────────

    [HttpGet("{id:int}/courtpairs")]
    public async Task<ActionResult<List<CourtPairDto>>> GetCourtPairs(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var pairs = await db.CourtPairs
            .Where(cp => cp.CompetitionId == id)
            .Include(cp => cp.Court1)
            .Include(cp => cp.Court2)
            .OrderBy(cp => cp.Name)
            .ToListAsync();
        return pairs.Select(cp => new CourtPairDto(
            cp.CourtPairId, cp.CompetitionId,
            cp.Court1Id, cp.Court1.Name,
            cp.Court2Id, cp.Court2.Name,
            cp.Name)).ToList();
    }

    [HttpPost("{id:int}/courtpairs")]
    public async Task<ActionResult<CourtPairDto>> AddCourtPair(int id, [FromBody] SaveCourtPairRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = new CourtPair
        {
            CompetitionId = id,
            Court1Id = req.Court1Id,
            Court2Id = req.Court2Id,
            Name = req.Name.Trim()
        };
        db.CourtPairs.Add(cp);
        await db.SaveChangesAsync();

        await db.Entry(cp).Reference(c => c.Court1).LoadAsync();
        await db.Entry(cp).Reference(c => c.Court2).LoadAsync();
        return new CourtPairDto(cp.CourtPairId, cp.CompetitionId,
            cp.Court1Id, cp.Court1.Name, cp.Court2Id, cp.Court2.Name, cp.Name);
    }

    [HttpPut("{id:int}/courtpairs/{courtPairId:int}")]
    public async Task<IActionResult> UpdateCourtPair(int id, int courtPairId, [FromBody] SaveCourtPairRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CourtPairs.FindAsync(courtPairId);
        if (cp is null || cp.CompetitionId != id) return NotFound();
        cp.Court1Id = req.Court1Id;
        cp.Court2Id = req.Court2Id;
        cp.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}/courtpairs/{courtPairId:int}")]
    public async Task<IActionResult> DeleteCourtPair(int id, int courtPairId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CourtPairs.FindAsync(courtPairId);
        if (cp is null || cp.CompetitionId != id) return NotFound();
        db.CourtPairs.Remove(cp);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Rubbers ──────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/fixtures/{fixtureId:int}/rubbers")]
    public async Task<ActionResult<List<RubberDto>>> GetRubbers(int id, int fixtureId)
    {
        await using var db = dbFactory.CreateDbContext();
        var rubbers = await db.Rubbers
            .Where(r => r.CompetitionFixtureId == fixtureId && r.Fixture.CompetitionId == id)
            .Include(r => r.HomePlayer1)
            .Include(r => r.HomePlayer2)
            .Include(r => r.AwayPlayer1)
            .Include(r => r.AwayPlayer2)
            .OrderBy(r => r.Order)
            .ToListAsync();

        return rubbers.Select(r => new RubberDto(
            r.RubberId, r.CompetitionFixtureId, r.Order, r.Name, r.CourtNumber,
            r.HomeRoles, r.AwayRoles,
            r.HomePlayer1Id, r.HomePlayer1?.DisplayName,
            r.HomePlayer2Id, r.HomePlayer2?.DisplayName,
            r.AwayPlayer1Id, r.AwayPlayer1?.DisplayName,
            r.AwayPlayer2Id, r.AwayPlayer2?.DisplayName,
            r.HomeGames, r.AwayGames, r.WinnerTeamId,
            r.IsComplete, r.SavedMatchId)).ToList();
    }

    // ── Team League Table ────────────────────────────────────────────────────

    [HttpGet("{id:int}/teamleaguetable")]
    public async Task<ActionResult<List<TeamLeagueTableEntryDto>>> GetTeamLeagueTable(
        int id, [FromQuery] int? divisionId = null)
    {
        await using var db = dbFactory.CreateDbContext();

        var comp = await db.Competition.AsNoTracking().FirstOrDefaultAsync(c => c.CompetitionID == id);
        var scoringMode = comp?.LeagueScoring ?? DropShot.Models.LeagueScoringMode.WinPoints;

        var rrStageIds = await db.CompetitionStages
            .Where(s => s.CompetitionId == id && s.StageType == DropShot.Models.StageType.RoundRobin)
            .Select(s => s.CompetitionStageId)
            .ToListAsync();

        if (rrStageIds.Count == 0) return Ok(new List<TeamLeagueTableEntryDto>());

        var teamsQuery = db.CompetitionTeams.Where(t => t.CompetitionId == id);
        if (divisionId.HasValue)
            teamsQuery = teamsQuery.Where(t => t.CompetitionDivisionId == divisionId.Value);
        var teams = await teamsQuery.Include(t => t.Captain).ToListAsync();
        var teamIds = teams.Select(t => t.CompetitionTeamId).ToHashSet();

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id
                && f.CompetitionStageId != null
                && rrStageIds.Contains(f.CompetitionStageId!.Value)
                && f.Status == DropShot.Models.FixtureStatus.Completed
                && f.HomeTeamId != null && f.AwayTeamId != null
                && teamIds.Contains(f.HomeTeamId.Value)
                && teamIds.Contains(f.AwayTeamId.Value))
            .Include(f => f.Rubbers)
            .ToListAsync();

        var played = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var won = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var drawn = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var lost = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var rubbersWon = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var rubbersAgainst = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var scoringFor = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var scoringAgainst = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var points = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);

        string unitLabel = scoringMode switch
        {
            DropShot.Models.LeagueScoringMode.SetsWon  => "sets",
            DropShot.Models.LeagueScoringMode.GamesWon => "games",
            _                                          => "rubbers",
        };

        foreach (var f in fixtures)
        {
            int homeId = f.HomeTeamId!.Value;
            int awayId = f.AwayTeamId!.Value;
            if (!played.ContainsKey(homeId) || !played.ContainsKey(awayId)) continue;

            var completed = f.Rubbers.Where(r => r.IsComplete).ToList();
            int homeRubbers = completed.Count(r => r.WinnerTeamId == homeId);
            int awayRubbers = completed.Count(r => r.WinnerTeamId == awayId);
            int homeSets = completed.Sum(r => r.HomeSetsWon ?? 0);
            int awaySets = completed.Sum(r => r.AwaySetsWon ?? 0);
            int homeGames = completed.Sum(r => r.HomeGamesTotal ?? 0);
            int awayGames = completed.Sum(r => r.AwayGamesTotal ?? 0);

            played[homeId]++;
            played[awayId]++;
            rubbersWon[homeId] += homeRubbers;
            rubbersAgainst[homeId] += awayRubbers;
            rubbersWon[awayId] += awayRubbers;
            rubbersAgainst[awayId] += homeRubbers;

            (int homeFor, int awayFor) = scoringMode switch
            {
                DropShot.Models.LeagueScoringMode.SetsWon  => (homeSets, awaySets),
                DropShot.Models.LeagueScoringMode.GamesWon => (homeGames, awayGames),
                _                                          => (homeRubbers, awayRubbers),
            };
            scoringFor[homeId] += homeFor;
            scoringAgainst[homeId] += awayFor;
            scoringFor[awayId] += awayFor;
            scoringAgainst[awayId] += homeFor;

            bool homeWin = homeRubbers > awayRubbers;
            bool awayWin = awayRubbers > homeRubbers;
            if (homeWin) { won[homeId]++; lost[awayId]++; }
            else if (awayWin) { won[awayId]++; lost[homeId]++; }
            else { drawn[homeId]++; drawn[awayId]++; }

            (int homePts, int awayPts) = scoringMode switch
            {
                DropShot.Models.LeagueScoringMode.SetsWon  => (homeSets, awaySets),
                DropShot.Models.LeagueScoringMode.GamesWon => (homeGames, awayGames),
                _ => (homeWin ? 3 : (!awayWin ? 1 : 0),
                      awayWin ? 3 : (!homeWin ? 1 : 0)),
            };
            points[homeId] += homePts;
            points[awayId] += awayPts;
        }

        var entries = teams
            .Select(t => new TeamLeagueTableEntryDto(
                t.CompetitionTeamId, t.Name, t.Captain?.DisplayName,
                played[t.CompetitionTeamId], won[t.CompetitionTeamId],
                drawn[t.CompetitionTeamId], lost[t.CompetitionTeamId],
                rubbersWon[t.CompetitionTeamId], rubbersAgainst[t.CompetitionTeamId],
                points[t.CompetitionTeamId],
                scoringFor[t.CompetitionTeamId], scoringAgainst[t.CompetitionTeamId],
                unitLabel))
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.ScoringFor - e.ScoringAgainst)
            .ThenBy(e => e.ScoringAgainst)
            .ToList();

        return entries;
    }

    // ── Divisions (ranked tiers within a competition) ───────────────────────

    [HttpGet("{id:int}/divisions")]
    public async Task<ActionResult<List<CompetitionDivisionDto>>> GetDivisions(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.CompetitionDivisions.AsNoTracking()
            .Where(d => d.CompetitionId == id)
            .OrderBy(d => d.Rank)
            .Select(d => new CompetitionDivisionDto(d.CompetitionDivisionId, d.CompetitionId, d.Rank, d.Name))
            .ToListAsync();
    }

    [HttpPost("{id:int}/divisions")]
    public async Task<ActionResult<CompetitionDivisionDto>> AddDivision(int id, [FromBody] SaveDivisionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Division name is required." });

        // Auto-rank: append to the end if rank is 0 / unspecified.
        byte rank = req.Rank;
        if (rank == 0)
        {
            var existing = await db.CompetitionDivisions
                .Where(d => d.CompetitionId == id)
                .Select(d => (int)d.Rank)
                .ToListAsync();
            rank = (byte)(existing.Count == 0 ? 1 : existing.Max() + 1);
        }
        else if (await db.CompetitionDivisions.AnyAsync(d => d.CompetitionId == id && d.Rank == rank))
        {
            return BadRequest(new { message = $"Division rank {rank} already exists." });
        }

        var division = new CompetitionDivision
        {
            CompetitionId = id,
            Rank = rank,
            Name = req.Name.Trim(),
        };
        db.CompetitionDivisions.Add(division);
        if (!comp.HasDivisions) comp.HasDivisions = true;
        await db.SaveChangesAsync();
        return new CompetitionDivisionDto(division.CompetitionDivisionId, id, division.Rank, division.Name);
    }

    [HttpPut("{id:int}/divisions/{divisionId:int}")]
    public async Task<IActionResult> UpdateDivision(int id, int divisionId, [FromBody] SaveDivisionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var d = await db.CompetitionDivisions.FindAsync(divisionId);
        if (d is null || d.CompetitionId != id) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.Name)) d.Name = req.Name.Trim();
        if (req.Rank > 0 && req.Rank != d.Rank)
        {
            if (await db.CompetitionDivisions.AnyAsync(x => x.CompetitionId == id && x.Rank == req.Rank && x.CompetitionDivisionId != divisionId))
                return BadRequest(new { message = $"Division rank {req.Rank} already exists." });
            d.Rank = req.Rank;
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}/divisions/{divisionId:int}")]
    public async Task<IActionResult> DeleteDivision(int id, int divisionId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var d = await db.CompetitionDivisions.FindAsync(divisionId);
        if (d is null || d.CompetitionId != id) return NotFound();
        // Null out anyone assigned to this division so they fall back to "unassigned".
        var participants = await db.CompetitionParticipants.Where(p => p.CompetitionDivisionId == divisionId).ToListAsync();
        foreach (var p in participants) p.CompetitionDivisionId = null;
        var teams = await db.CompetitionTeams.Where(t => t.CompetitionDivisionId == divisionId).ToListAsync();
        foreach (var t in teams) t.CompetitionDivisionId = null;
        db.CompetitionDivisions.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/participants/{playerId:int}/division")]
    public async Task<IActionResult> SetParticipantDivision(int id, int playerId, [FromBody] SetParticipantDivisionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();

        if (req.CompetitionDivisionId.HasValue)
        {
            var d = await db.CompetitionDivisions.FindAsync(req.CompetitionDivisionId.Value);
            if (d is null || d.CompetitionId != id)
                return BadRequest(new { message = "Division belongs to another competition." });
        }

        cp.CompetitionDivisionId = req.CompetitionDivisionId;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id:int}/teams/{teamId:int}/division")]
    public async Task<IActionResult> SetTeamDivision(int id, int teamId, [FromBody] SetParticipantDivisionRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var team = await db.CompetitionTeams.FindAsync(teamId);
        if (team is null || team.CompetitionId != id) return NotFound();
        if (req.CompetitionDivisionId.HasValue)
        {
            var d = await db.CompetitionDivisions.FindAsync(req.CompetitionDivisionId.Value);
            if (d is null || d.CompetitionId != id)
                return BadRequest(new { message = "Division belongs to another competition." });
        }
        team.CompetitionDivisionId = req.CompetitionDivisionId;
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Copy the previous competition's division structure (names + ranks) to this
    /// competition, and (when overlapping participants exist) place each returning
    /// player into the same-ranked division. Optionally apply promotion / demotion
    /// counts based on each previous division's final standings.
    /// </summary>
    [HttpPost("{id:int}/seed-divisions-from-previous")]
    public async Task<ActionResult<SeedDivisionsResultDto>> SeedDivisionsFromPrevious(int id, [FromBody] SeedDivisionsFromPreviousRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition
            .Include(c => c.Divisions)
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.CompetitionID == id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var previous = await db.Competition
            .Include(c => c.Divisions)
            .Include(c => c.Participants).ThenInclude(p => p.Division)
            .Include(c => c.Fixtures).ThenInclude(f => f.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == req.PreviousCompetitionId);
        if (previous is null) return BadRequest(new { message = "Previous competition not found." });

        if (comp.Divisions.Any())
            return BadRequest(new { message = "This competition already has divisions — clear them first." });

        // Recreate division shells in this competition based on the previous
        // structure (preserving rank + name).
        var newDivisionsByRank = new Dictionary<byte, CompetitionDivision>();
        foreach (var prevDiv in previous.Divisions.OrderBy(d => d.Rank))
        {
            var d = new CompetitionDivision
            {
                CompetitionId = id,
                Rank = prevDiv.Rank,
                Name = prevDiv.Name,
            };
            db.CompetitionDivisions.Add(d);
            newDivisionsByRank[prevDiv.Rank] = d;
        }
        comp.HasDivisions = true;
        comp.SeededFromCompetitionId = previous.CompetitionID;
        await db.SaveChangesAsync();

        // Compute desired next-season rank for each player from the previous standings.
        var previousRankByPlayer = ComputeNextSeasonRanks(previous, req);

        int assigned = 0;
        var thisCompPlayerIds = comp.Participants.Select(p => p.PlayerId).ToHashSet();
        foreach (var (playerId, nextRank) in previousRankByPlayer)
        {
            if (!thisCompPlayerIds.Contains(playerId)) continue;
            if (!newDivisionsByRank.TryGetValue(nextRank, out var division)) continue;
            var cp = comp.Participants.First(p => p.PlayerId == playerId);
            cp.CompetitionDivisionId = division.CompetitionDivisionId;
            assigned++;
        }
        await db.SaveChangesAsync();
        return new SeedDivisionsResultDto(newDivisionsByRank.Count, assigned);
    }

    /// <summary>
    /// Returns a map of PlayerId → next-season division rank, derived from the
    /// previous competition's per-player ranking inside their division. When
    /// promote/demote counts are non-zero, top-N of each division get promoted
    /// (rank-1) and bottom-N get demoted (rank+1), within the available rank
    /// range; everyone else stays in their rank.
    /// </summary>
    private static Dictionary<int, byte> ComputeNextSeasonRanks(Competition previous, SeedDivisionsFromPreviousRequest req)
    {
        var result = new Dictionary<int, byte>();
        if (!previous.Divisions.Any()) return result;
        byte minRank = previous.Divisions.Min(d => d.Rank);
        byte maxRank = previous.Divisions.Max(d => d.Rank);
        var scoringMode = previous.LeagueScoring;

        foreach (var division in previous.Divisions)
        {
            // Collect rubbers for this division's competition only — that's the
            // whole previous competition (single-comp seed).
            var ranking = previous.Participants
                .Where(p => p.CompetitionDivisionId == division.CompetitionDivisionId)
                .Select(p =>
                {
                    var rubbers = previous.Fixtures
                        .SelectMany(f => f.Rubbers)
                        .Where(r => r.IsComplete &&
                                    (r.HomePlayer1Id == p.PlayerId || r.HomePlayer2Id == p.PlayerId
                                  || r.AwayPlayer1Id == p.PlayerId || r.AwayPlayer2Id == p.PlayerId))
                        .ToList();
                    int played = rubbers.Count;
                    int won = rubbers.Count(r =>
                    {
                        bool isHome = r.HomePlayer1Id == p.PlayerId || r.HomePlayer2Id == p.PlayerId;
                        return isHome ? r.WinnerTeamId == r.Fixture.HomeTeamId : r.WinnerTeamId == r.Fixture.AwayTeamId;
                    });
                    int sets = rubbers.Sum(r =>
                    {
                        bool isHome = r.HomePlayer1Id == p.PlayerId || r.HomePlayer2Id == p.PlayerId;
                        return isHome ? (r.HomeSetsWon ?? 0) : (r.AwaySetsWon ?? 0);
                    });
                    int games = rubbers.Sum(r =>
                    {
                        bool isHome = r.HomePlayer1Id == p.PlayerId || r.HomePlayer2Id == p.PlayerId;
                        return isHome ? (r.HomeGamesTotal ?? 0) : (r.AwayGamesTotal ?? 0);
                    });
                    int points = scoringMode switch
                    {
                        Models.LeagueScoringMode.SetsWon  => sets,
                        Models.LeagueScoringMode.GamesWon => games,
                        _ => won * 3, // approx — this is per-player not per-fixture
                    };
                    return (PlayerId: p.PlayerId, Played: played, Won: won, Points: points);
                })
                .OrderByDescending(r => r.Points)
                .ThenByDescending(r => r.Won)
                .ToList();

            int promote = req.ApplyPromotion ? Math.Max(0, req.PromoteCount) : 0;
            int demote  = req.ApplyPromotion ? Math.Max(0, req.DemoteCount) : 0;
            for (int i = 0; i < ranking.Count; i++)
            {
                byte targetRank = division.Rank;
                if (i < promote && division.Rank > minRank) targetRank = (byte)(division.Rank - 1);
                else if (i >= ranking.Count - demote && division.Rank < maxRank) targetRank = (byte)(division.Rank + 1);
                result[ranking[i].PlayerId] = targetRank;
            }
        }
        return result;
    }

    private static string StageDisplayName(Models.StageType type) => type switch
    {
        Models.StageType.RoundRobin   => "Round Robin",
        Models.StageType.Knockout     => "Knockout",
        Models.StageType.QuarterFinal => "Quarter-Final",
        Models.StageType.SemiFinal    => "Semi-Final",
        Models.StageType.Final        => "Final",
        _                             => type.ToString()
    };
}
