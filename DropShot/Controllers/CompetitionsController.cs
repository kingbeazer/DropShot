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
    public async Task<ActionResult<List<CompetitionDto>>> GetAll(
        [FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] int? eventId = null,
        [FromQuery] bool includeArchived = false)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .Include(c => c.Event)
            .AsQueryable();

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        if (eventId.HasValue)
            query = query.Where(c => c.EventId == eventId.Value);

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
            .FirstOrDefaultAsync(x => x.CompetitionID == id);

        if (c is null) return NotFound();

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
                (DropShot.Shared.PlayerGrade?)p.Grade,
                (DropShot.Shared.PlayerSex?)p.Player?.Sex)).ToList(),
            c.IsArchived);
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionDto>> Create([FromBody] SaveCompetitionRequest req)
    {
        if (!await authzService.CanEditCompetitionAsync(User, req.HostClubId))
            return Forbid();

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
        var teamMatchSets = await db.TeamMatchSets
            .Where(s => s.Fixture.CompetitionId == id)
            .ToListAsync();
        db.TeamMatchSets.RemoveRange(teamMatchSets);

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

        // Check for downstream fixtures that have already progressed beyond Scheduled
        if (oldWinnerPlayerId.HasValue || oldWinnerTeamId.HasValue)
        {
            var downstreamFixtures = await db.CompetitionFixtures
                .Where(f => f.CompetitionId == id
                    && f.CompetitionFixtureId != fixtureId
                    && (f.Status == FixtureStatus.InProgress || f.Status == FixtureStatus.Completed
                        || f.Status == FixtureStatus.AwaitingVerification))
                .ToListAsync();

            bool blocked = isTeamFixture
                ? downstreamFixtures.Any(f =>
                    f.HomeTeamId == oldWinnerTeamId || f.AwayTeamId == oldWinnerTeamId)
                : downstreamFixtures.Any(f =>
                    f.Player1Id == oldWinnerPlayerId || f.Player2Id == oldWinnerPlayerId);

            if (blocked)
                return BadRequest(new { message = "Cannot delete this result because the winner has already been placed in a downstream match that is in progress or completed. Delete that result first." });
        }

        // Clear winner from any next-round Scheduled fixtures
        if (oldWinnerPlayerId.HasValue)
        {
            var nextFixtures = await db.CompetitionFixtures
                .Where(f => f.CompetitionId == id
                    && f.CompetitionFixtureId != fixtureId
                    && f.Status == FixtureStatus.Scheduled
                    && (f.Player1Id == oldWinnerPlayerId || f.Player2Id == oldWinnerPlayerId))
                .ToListAsync();

            foreach (var nf in nextFixtures)
            {
                if (nf.Player1Id == oldWinnerPlayerId) nf.Player1Id = null;
                if (nf.Player2Id == oldWinnerPlayerId) nf.Player2Id = null;
            }
        }

        if (oldWinnerTeamId.HasValue)
        {
            var nextFixtures = await db.CompetitionFixtures
                .Include(f => f.TeamMatchSets)
                .Where(f => f.CompetitionId == id
                    && f.CompetitionFixtureId != fixtureId
                    && f.Status == FixtureStatus.Scheduled
                    && (f.HomeTeamId == oldWinnerTeamId || f.AwayTeamId == oldWinnerTeamId))
                .ToListAsync();

            foreach (var nf in nextFixtures)
            {
                if (nf.HomeTeamId == oldWinnerTeamId) nf.HomeTeamId = null;
                if (nf.AwayTeamId == oldWinnerTeamId) nf.AwayTeamId = null;
                // Remove auto-generated TeamMatchSets for the cleared fixture
                if (nf.TeamMatchSets.Any())
                    db.TeamMatchSets.RemoveRange(nf.TeamMatchSets);
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

        foreach (var stage in comp.Stages)
        {
            switch (stage.StageType)
            {
                case DropShot.Models.StageType.RoundRobin:
                {
                    if (comp.CompetitionFormat == DropShot.Models.CompetitionFormat.MixedTeam)
                    {
                        // Team round-robin using circle method
                        var teamIds = comp.Teams.Select(t => t.CompetitionTeamId).ToList();
                        var courtPairIds = comp.CourtPairs.Select(cp => cp.CourtPairId).ToList();
                        int cpIdx = 0;

                        // Circle method: fix team[0], rotate rest
                        int n = teamIds.Count;
                        bool hasOdd = n % 2 != 0;
                        if (hasOdd) teamIds.Add(-1); // BYE sentinel
                        int total = teamIds.Count;
                        int rounds = total - 1;

                        // Load all team members for set creation
                        var allMembers = comp.Participants
                            .Where(p => p.TeamId != null && p.Player != null
                                && (p.Status == DropShot.Models.ParticipantStatus.Registered
                                    || p.Status == DropShot.Models.ParticipantStatus.Confirmed))
                            .ToList();

                        for (int round = 0; round < rounds; round++)
                        {
                            for (int match = 0; match < total / 2; match++)
                            {
                                int home = (match == 0) ? 0 : (round + match) % (total - 1) + 1;
                                int away = (total - 1) - match;
                                if (match == 0) away = (round % (total - 1)) + 1;

                                // Correct circle method indices
                                home = match == 0 ? 0 : ((round + match - 1) % (total - 1)) + 1;
                                away = ((round + total - 1 - match) % (total - 1)) + 1;
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

                                db.CompetitionFixtures.Add(fixture);
                                await db.SaveChangesAsync(); // Need ID for TeamMatchSets

                                // Create 8 TeamMatchSet rows
                                var homeMembers = allMembers.Where(m => m.TeamId == homeTeamId).ToList();
                                var awayMembers = allMembers.Where(m => m.TeamId == awayTeamId).ToList();

                                if (homeMembers.Count == 4 && awayMembers.Count == 4
                                    && homeMembers.All(m => m.Grade != null && m.Player?.Sex != null)
                                    && awayMembers.All(m => m.Grade != null && m.Player?.Sex != null))
                                {
                                    var sets = TeamMatchService.CreateSetsForFixture(
                                        fixture.CompetitionFixtureId, homeMembers, awayMembers);
                                    db.TeamMatchSets.AddRange(sets);
                                }
                            }
                        }
                    }
                    else
                    {
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
                    int n = comp.CompetitionFormat == DropShot.Models.CompetitionFormat.MixedTeam
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
        c.HostClubId = r.HostClubId;
        c.RulesSetId = r.RulesSetId;
        c.EventId = r.EventId;
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
        c.EventId, c.Event?.Name, c.IsArchived);

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

    // ── Mixed Team Tennis Endpoints ──────────────────────────────────────────

    [HttpPut("{id:int}/participants/{playerId:int}/grade")]
    public async Task<IActionResult> SetParticipantGrade(
        int id, int playerId, [FromBody] SetParticipantGradeRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        cp.Grade = (DropShot.Models.PlayerGrade)req.Grade;
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
        var members = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == id && cp.TeamId == teamId
                && (cp.Status == DropShot.Models.ParticipantStatus.Registered
                    || cp.Status == DropShot.Models.ParticipantStatus.Confirmed))
            .Include(cp => cp.Player)
            .ToListAsync();

        var errors = new List<string>();
        if (members.Count != 4)
            errors.Add($"Team must have exactly 4 active members (has {members.Count}).");

        foreach (var m in members)
        {
            if (m.Player?.Sex == null) errors.Add($"{m.Player?.DisplayName ?? "Unknown"} has no sex assigned.");
            if (m.Grade == null) errors.Add($"{m.Player?.DisplayName ?? "Unknown"} has no grade assigned.");
        }

        if (errors.Count == 0)
        {
            var maleA = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Male && m.Grade == DropShot.Models.PlayerGrade.A);
            var femaleA = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Female && m.Grade == DropShot.Models.PlayerGrade.A);
            var maleB = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Male && m.Grade == DropShot.Models.PlayerGrade.B);
            var femaleB = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Female && m.Grade == DropShot.Models.PlayerGrade.B);

            if (maleA != 1) errors.Add($"Need exactly 1 Male A player (has {maleA}).");
            if (femaleA != 1) errors.Add($"Need exactly 1 Female A player (has {femaleA}).");
            if (maleB != 1) errors.Add($"Need exactly 1 Male B player (has {maleB}).");
            if (femaleB != 1) errors.Add($"Need exactly 1 Female B player (has {femaleB}).");
        }

        return new TeamValidationResultDto(errors.Count == 0, errors);
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

    // ── Team Match Sets ──────────────────────────────────────────────────────

    [HttpGet("{id:int}/fixtures/{fixtureId:int}/sets")]
    public async Task<ActionResult<List<TeamMatchSetDto>>> GetTeamMatchSets(int id, int fixtureId)
    {
        await using var db = dbFactory.CreateDbContext();
        var sets = await db.TeamMatchSets
            .Where(s => s.CompetitionFixtureId == fixtureId && s.Fixture.CompetitionId == id)
            .Include(s => s.HomePlayer1)
            .Include(s => s.HomePlayer2)
            .Include(s => s.AwayPlayer1)
            .Include(s => s.AwayPlayer2)
            .OrderBy(s => s.SetNumber)
            .ToListAsync();

        return sets.Select(s => new TeamMatchSetDto(
            s.TeamMatchSetId, s.CompetitionFixtureId, s.SetNumber,
            (DropShot.Shared.TeamMatchPhase)s.Phase,
            (DropShot.Shared.TeamMatchSetType)s.SetType,
            s.CourtNumber,
            s.HomePlayer1Id, s.HomePlayer1?.DisplayName,
            s.HomePlayer2Id, s.HomePlayer2?.DisplayName,
            s.AwayPlayer1Id, s.AwayPlayer1?.DisplayName,
            s.AwayPlayer2Id, s.AwayPlayer2?.DisplayName,
            s.HomeGames, s.AwayGames, s.WinnerTeamId,
            s.IsComplete, s.SavedMatchId)).ToList();
    }

    // ── Team League Table ────────────────────────────────────────────────────

    [HttpGet("{id:int}/teamleaguetable")]
    public async Task<ActionResult<List<TeamLeagueTableEntryDto>>> GetTeamLeagueTable(int id)
    {
        await using var db = dbFactory.CreateDbContext();

        var rrStageIds = await db.CompetitionStages
            .Where(s => s.CompetitionId == id && s.StageType == DropShot.Models.StageType.RoundRobin)
            .Select(s => s.CompetitionStageId)
            .ToListAsync();

        if (rrStageIds.Count == 0) return Ok(new List<TeamLeagueTableEntryDto>());

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id
                && f.CompetitionStageId != null
                && rrStageIds.Contains(f.CompetitionStageId!.Value)
                && f.Status == DropShot.Models.FixtureStatus.Completed
                && f.HomeTeamId != null && f.AwayTeamId != null)
            .Include(f => f.TeamMatchSets)
            .ToListAsync();

        var teams = await db.CompetitionTeams
            .Where(t => t.CompetitionId == id)
            .Include(t => t.Captain)
            .ToListAsync();

        var stats = teams.ToDictionary(t => t.CompetitionTeamId, t => new
        {
            t.Name,
            CaptainName = t.Captain?.DisplayName,
            Played = 0, Won = 0, Drawn = 0, Lost = 0, SetsWon = 0, SetsAgainst = 0
        });

        // Use mutable counters
        var played = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var won = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var drawn = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var lost = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var setsWon = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);
        var setsAgainst = teams.ToDictionary(t => t.CompetitionTeamId, _ => 0);

        foreach (var f in fixtures)
        {
            int homeId = f.HomeTeamId!.Value;
            int awayId = f.AwayTeamId!.Value;
            if (!played.ContainsKey(homeId) || !played.ContainsKey(awayId)) continue;

            var completedSets = f.TeamMatchSets.Where(s => s.IsComplete).ToList();
            int homeSetsWon = completedSets.Count(s => s.WinnerTeamId == homeId);
            int awaySetsWon = completedSets.Count(s => s.WinnerTeamId == awayId);

            played[homeId]++;
            played[awayId]++;
            setsWon[homeId] += homeSetsWon;
            setsAgainst[homeId] += awaySetsWon;
            setsWon[awayId] += awaySetsWon;
            setsAgainst[awayId] += homeSetsWon;

            if (homeSetsWon > awaySetsWon) { won[homeId]++; lost[awayId]++; }
            else if (awaySetsWon > homeSetsWon) { won[awayId]++; lost[homeId]++; }
            else { drawn[homeId]++; drawn[awayId]++; }
        }

        var entries = teams
            .Select(t => new TeamLeagueTableEntryDto(
                t.CompetitionTeamId, t.Name, t.Captain?.DisplayName,
                played[t.CompetitionTeamId], won[t.CompetitionTeamId],
                drawn[t.CompetitionTeamId], lost[t.CompetitionTeamId],
                setsWon[t.CompetitionTeamId], setsAgainst[t.CompetitionTeamId],
                setsWon[t.CompetitionTeamId]))
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.SetsWon - e.SetsAgainst)
            .ThenBy(e => e.SetsAgainst)
            .ToList();

        return entries;
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
