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
        [FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] int? eventId = null)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .Include(c => c.Event)
            .AsQueryable();

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
                (DropShot.Shared.PlayerGrade?)p.Grade)).ToList());
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

    [HttpPut("{id:int}/participants/{playerId:int}/grade")]
    public async Task<IActionResult> UpdateParticipantGrade(
        int id, int playerId, [FromBody] UpdateParticipantGradeRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var cp = await db.CompetitionParticipants.FindAsync(id, playerId);
        if (cp is null) return NotFound();
        cp.Grade = req.Grade.HasValue ? (DropShot.Models.PlayerGrade)req.Grade.Value : null;
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

    [HttpPut("{id:int}/teams/{teamId:int}/captain")]
    public async Task<IActionResult> UpdateTeamCaptain(
        int id, int teamId, [FromBody] UpdateTeamCaptainRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var team = await db.CompetitionTeams.FindAsync(teamId);
        if (team is null || team.CompetitionId != id) return NotFound();

        if (req.CaptainPlayerId.HasValue)
        {
            var isMember = await db.CompetitionParticipants
                .AnyAsync(cp => cp.CompetitionId == id && cp.PlayerId == req.CaptainPlayerId.Value && cp.TeamId == teamId);
            if (!isMember)
                return BadRequest(new { message = "Captain must be a member of the team." });
        }

        team.CaptainPlayerId = req.CaptainPlayerId;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/teams/{teamId:int}/validate")]
    public async Task<IActionResult> ValidateTeamComposition(int id, int teamId)
    {
        await using var db = dbFactory.CreateDbContext();
        var members = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == id && cp.TeamId == teamId)
            .Include(cp => cp.Player)
            .ToListAsync();

        var errors = ValidateMixedTeamComposition(members);
        if (errors.Count > 0)
            return BadRequest(new { errors });
        return Ok(new { message = "Team composition is valid." });
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
    public async Task<IActionResult> AddCourtPair(int id, [FromBody] SaveCourtPairRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        if (req.Court1Id == req.Court2Id)
            return BadRequest(new { message = "Court 1 and Court 2 must be different." });

        var court1 = await db.Courts.FindAsync(req.Court1Id);
        var court2 = await db.Courts.FindAsync(req.Court2Id);
        if (court1 is null || court2 is null)
            return BadRequest(new { message = "One or more court IDs are invalid." });

        var pair = new CourtPair
        {
            CompetitionId = id,
            Court1Id = req.Court1Id,
            Court2Id = req.Court2Id,
            Name = req.Name.Trim()
        };
        db.CourtPairs.Add(pair);
        await db.SaveChangesAsync();
        return Ok(new CourtPairDto(
            pair.CourtPairId, pair.CompetitionId,
            pair.Court1Id, court1.Name,
            pair.Court2Id, court2.Name,
            pair.Name));
    }

    [HttpDelete("{id:int}/courtpairs/{pairId:int}")]
    public async Task<IActionResult> DeleteCourtPair(int id, int pairId)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        var pair = await db.CourtPairs.FindAsync(pairId);
        if (pair is null || pair.CompetitionId != id) return NotFound();
        db.CourtPairs.Remove(pair);
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

    [HttpPost("{id:int}/fixtures/schedule")]
    public async Task<IActionResult> ScheduleFixtures(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition
            .Include(c => c.Stages.OrderBy(s => s.StageOrder))
            .Include(c => c.Participants).ThenInclude(p => p.Player)
            .Include(c => c.Teams)
            .Include(c => c.MatchWindows).ThenInclude(w => w.Court)
            .Include(c => c.CourtPairs)
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

        bool isMixedTeam = comp.CompetitionFormat == DropShot.Models.CompetitionFormat.MixedTeam;

        if (isMixedTeam)
        {
            var teamList = comp.Teams.ToList();
            var courtPairs = comp.CourtPairs.ToList();
            int courtPairIdx = 0;

            CompetitionFixture NewTeamFixture(int compId, int stageId)
            {
                var slot = SchedulingSlotPicker.PickSlot(matchWindows, startDate, endDate, rng);
                var f = new CompetitionFixture
                {
                    CompetitionId = compId,
                    CompetitionStageId = stageId,
                    ScheduledAt = slot,
                    Status = DropShot.Models.FixtureStatus.Scheduled
                };
                if (courtPairs.Count > 0)
                {
                    f.CourtPairId = courtPairs[courtPairIdx % courtPairs.Count].CourtPairId;
                    courtPairIdx++;
                }
                return f;
            }

            // Build participant lookup by team for auto-assigning set players
            var participantsByTeam = comp.Participants
                .Where(p => p.TeamId.HasValue && p.Grade.HasValue && p.Player?.Sex != null)
                .GroupBy(p => p.TeamId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var stage in comp.Stages)
            {
                switch (stage.StageType)
                {
                    case DropShot.Models.StageType.RoundRobin:
                    {
                        // Round-robin: each team plays every other team once
                        // Use circle method for scheduling rounds (handles odd teams with a bye)
                        var teams = teamList.Select(t => t.CompetitionTeamId).ToList();
                        int n = teams.Count;
                        bool hasBye = n % 2 != 0;
                        if (hasBye) teams.Add(-1); // sentinel for bye
                        int totalTeams = teams.Count;
                        int rounds = totalTeams - 1;

                        for (int round = 0; round < rounds; round++)
                        {
                            for (int match = 0; match < totalTeams / 2; match++)
                            {
                                int home = teams[match];
                                int away = teams[totalTeams - 1 - match];

                                if (home == -1 || away == -1) continue; // bye round

                                var fixture = NewTeamFixture(id, stage.CompetitionStageId);
                                fixture.HomeTeamId = home;
                                fixture.AwayTeamId = away;
                                fixture.RoundNumber = round + 1;
                                fixture.FixtureLabel = $"Round {round + 1}";
                                db.CompetitionFixtures.Add(fixture);

                                // Create 8 TeamMatchSet rows
                                CreateTeamMatchSets(fixture, home, away, participantsByTeam);
                            }

                            // Rotate: fix position 0, rotate the rest
                            var last = teams[totalTeams - 1];
                            for (int k = totalTeams - 1; k > 1; k--)
                                teams[k] = teams[k - 1];
                            teams[1] = last;
                        }
                        break;
                    }

                    case DropShot.Models.StageType.Knockout:
                    {
                        // Top 4 → SF + Final
                        int n = teamList.Count;
                        if (n < 2) break;

                        for (int m = 0; m < 2; m++)
                        {
                            var sf = NewTeamFixture(id, stage.CompetitionStageId);
                            sf.FixtureLabel = $"Semi-Final {m + 1}";
                            sf.RoundNumber = 1;
                            db.CompetitionFixtures.Add(sf);
                        }

                        var final1 = NewTeamFixture(id, stage.CompetitionStageId);
                        final1.FixtureLabel = "Final";
                        final1.RoundNumber = 2;
                        db.CompetitionFixtures.Add(final1);
                        break;
                    }

                    case DropShot.Models.StageType.SemiFinal:
                    {
                        for (int m = 0; m < 2; m++)
                        {
                            var sf = NewTeamFixture(id, stage.CompetitionStageId);
                            sf.FixtureLabel = $"Semi-Final {m + 1}";
                            sf.RoundNumber = 1;
                            db.CompetitionFixtures.Add(sf);
                        }
                        break;
                    }

                    case DropShot.Models.StageType.Final:
                    {
                        var final2 = NewTeamFixture(id, stage.CompetitionStageId);
                        final2.FixtureLabel = "Final";
                        final2.RoundNumber = 1;
                        db.CompetitionFixtures.Add(final2);
                        break;
                    }
                }
            }
        }
        else
        {
            // Standard player-based scheduling (Singles, Doubles, etc.)
            foreach (var stage in comp.Stages)
            {
                switch (stage.StageType)
                {
                    case DropShot.Models.StageType.RoundRobin:
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
                        break;
                    }

                    case DropShot.Models.StageType.Knockout:
                    {
                        int n = activePlayers.Count;
                        if (n < 2) break;

                        if (n >= 8)
                        {
                            for (int m = 0; m < 4; m++)
                            {
                                var qf = NewFixture(id, stage.CompetitionStageId);
                                qf.FixtureLabel = $"Quarter-Final {m + 1}";
                                qf.RoundNumber = 1;
                                db.CompetitionFixtures.Add(qf);
                            }
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
                            for (int m = 0; m < 2; m++)
                            {
                                var sf = NewFixture(id, stage.CompetitionStageId);
                                sf.FixtureLabel = $"Semi-Final {m + 1}";
                                sf.RoundNumber = 1;
                                db.CompetitionFixtures.Add(sf);
                            }
                        }

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

    // ── Team League Table ──────────────────────────────────────────────────────

    [HttpGet("{id:int}/teamleaguetable")]
    public async Task<ActionResult<List<TeamLeagueTableEntryDto>>> GetTeamLeagueTable(int id)
    {
        await using var db = dbFactory.CreateDbContext();

        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();

        var rrStageIds = await db.CompetitionStages
            .Where(s => s.CompetitionId == id && s.StageType == DropShot.Models.StageType.RoundRobin)
            .Select(s => s.CompetitionStageId)
            .ToListAsync();

        var teams = await db.CompetitionTeams
            .Where(t => t.CompetitionId == id)
            .Include(t => t.Captain)
            .ToListAsync();

        if (rrStageIds.Count == 0 || teams.Count == 0)
            return Ok(new List<TeamLeagueTableEntryDto>());

        // Load completed RR fixtures with their TeamMatchSets
        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id
                        && f.CompetitionStageId != null
                        && rrStageIds.Contains(f.CompetitionStageId!.Value)
                        && f.Status == DropShot.Models.FixtureStatus.Completed
                        && f.HomeTeamId != null && f.AwayTeamId != null)
            .Include(f => f.TeamMatchSets)
            .ToListAsync();

        var teamIds = teams.Select(t => t.CompetitionTeamId).ToHashSet();
        var stats = teamIds.ToDictionary(tid => tid, _ => (Played: 0, Won: 0, Drawn: 0, Lost: 0, SetsWon: 0, SetsAgainst: 0));

        foreach (var f in fixtures)
        {
            int homeId = f.HomeTeamId!.Value;
            int awayId = f.AwayTeamId!.Value;
            if (!stats.ContainsKey(homeId) || !stats.ContainsKey(awayId)) continue;

            // Count sets won by each team in this fixture
            int homeSets = f.TeamMatchSets.Count(s => s.IsComplete && s.WinnerTeamId == homeId);
            int awaySets = f.TeamMatchSets.Count(s => s.IsComplete && s.WinnerTeamId == awayId);

            var h = stats[homeId];
            var a = stats[awayId];

            h.Played++;
            a.Played++;
            h.SetsWon += homeSets;
            h.SetsAgainst += awaySets;
            a.SetsWon += awaySets;
            a.SetsAgainst += homeSets;

            if (homeSets > awaySets) { h.Won++; a.Lost++; }
            else if (awaySets > homeSets) { a.Won++; h.Lost++; }
            else { h.Drawn++; a.Drawn++; }

            stats[homeId] = h;
            stats[awayId] = a;
        }

        // Head-to-head tiebreaker: (winner team, loser team)
        var h2h = new HashSet<(int winner, int loser)>();
        foreach (var f in fixtures)
        {
            int homeId = f.HomeTeamId!.Value;
            int awayId = f.AwayTeamId!.Value;
            int homeSets = f.TeamMatchSets.Count(s => s.IsComplete && s.WinnerTeamId == homeId);
            int awaySets = f.TeamMatchSets.Count(s => s.IsComplete && s.WinnerTeamId == awayId);
            if (homeSets > awaySets) h2h.Add((homeId, awayId));
            else if (awaySets > homeSets) h2h.Add((awayId, homeId));
        }

        // Points = total sets won
        var entries = teams
            .Where(t => stats.ContainsKey(t.CompetitionTeamId))
            .Select(t =>
            {
                var s = stats[t.CompetitionTeamId];
                return new TeamLeagueTableEntryDto(
                    t.CompetitionTeamId, t.Name, t.Captain?.DisplayName,
                    s.Played, s.Won, s.Drawn, s.Lost,
                    s.SetsWon, s.SetsAgainst, s.SetsWon);
            })
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.SetsWon - e.SetsAgainst)
            .ThenByDescending(e => h2h.Count(h => h.winner == e.TeamId))
            .ToList();

        return entries;
    }

    // ── Team Knockout Seeding ────────────────────────────────────────────────

    [HttpPost("{id:int}/fixtures/seed-team-knockout")]
    public async Task<IActionResult> SeedTeamKnockout(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var comp = await db.Competition.FindAsync(id);
        if (comp is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, comp.HostClubId)) return Forbid();

        // Get team league table standings
        var tableResult = await GetTeamLeagueTable(id);
        if (tableResult.Result is not OkObjectResult okResult) return tableResult.Result!;
        var table = (tableResult.Value ?? (okResult.Value as List<TeamLeagueTableEntryDto>))!;

        if (table.Count < 4)
            return BadRequest(new { message = "Need at least 4 teams for knockout seeding." });

        // Get top 4 team IDs
        var top4 = table.Take(4).Select(e => e.TeamId).ToList();

        // Find knockout/SF stage fixtures
        var allStages = await db.CompetitionStages
            .Where(s => s.CompetitionId == id)
            .OrderBy(s => s.StageOrder)
            .ToListAsync();

        var koStage = allStages.FirstOrDefault(s =>
            s.StageType is DropShot.Models.StageType.Knockout or DropShot.Models.StageType.SemiFinal);

        if (koStage is null)
            return BadRequest(new { message = "No knockout or semi-final stage found." });

        var sfFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == id
                     && f.CompetitionStageId == koStage.CompetitionStageId
                     && (koStage.StageType != DropShot.Models.StageType.Knockout || f.RoundNumber == 1)
                     && f.Status == DropShot.Models.FixtureStatus.Scheduled)
            .OrderBy(f => f.FixtureLabel)
            .ToListAsync();

        if (sfFixtures.Count < 2)
            return BadRequest(new { message = "Not enough unassigned semi-final fixtures." });

        // Seed: 1st vs 4th, 2nd vs 3rd
        sfFixtures[0].HomeTeamId = top4[0];
        sfFixtures[0].AwayTeamId = top4[3];
        sfFixtures[1].HomeTeamId = top4[1];
        sfFixtures[1].AwayTeamId = top4[2];

        // Load participants for creating TeamMatchSets
        var participantsByTeam = await db.CompetitionParticipants
            .Where(p => p.CompetitionId == id && p.TeamId.HasValue && p.Grade.HasValue)
            .Include(p => p.Player)
            .GroupBy(p => p.TeamId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.ToList());

        foreach (var sf in sfFixtures)
        {
            if (sf.HomeTeamId.HasValue && sf.AwayTeamId.HasValue)
                CreateTeamMatchSets(sf, sf.HomeTeamId.Value, sf.AwayTeamId.Value, participantsByTeam);
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Top 4 teams seeded into semi-finals." });
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
        c.EventId, c.Event?.Name);

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

    private static void CreateTeamMatchSets(
        CompetitionFixture fixture, int homeTeamId, int awayTeamId,
        Dictionary<int, List<CompetitionParticipant>> participantsByTeam)
    {
        participantsByTeam.TryGetValue(homeTeamId, out var homeMembers);
        participantsByTeam.TryGetValue(awayTeamId, out var awayMembers);

        var homeMaleA = homeMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Male && p.Grade == DropShot.Models.PlayerGrade.A);
        var homeFemaleA = homeMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Female && p.Grade == DropShot.Models.PlayerGrade.A);
        var homeMaleB = homeMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Male && p.Grade == DropShot.Models.PlayerGrade.B);
        var homeFemaleB = homeMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Female && p.Grade == DropShot.Models.PlayerGrade.B);

        var awayMaleA = awayMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Male && p.Grade == DropShot.Models.PlayerGrade.A);
        var awayFemaleA = awayMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Female && p.Grade == DropShot.Models.PlayerGrade.A);
        var awayMaleB = awayMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Male && p.Grade == DropShot.Models.PlayerGrade.B);
        var awayFemaleB = awayMembers?.FirstOrDefault(p => p.Player?.Sex == DropShot.Models.PlayerSex.Female && p.Grade == DropShot.Models.PlayerGrade.B);

        // Phase 1: Gender Doubles (Court 1 = Men's, Court 2 = Women's)
        // Set 1: Men's Doubles 1
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 1, Phase = DropShot.Models.TeamMatchPhase.GenderDoubles,
            SetType = DropShot.Models.TeamMatchSetType.MensDoubles, CourtNumber = 1,
            HomePlayer1Id = homeMaleA?.PlayerId, HomePlayer2Id = homeMaleB?.PlayerId,
            AwayPlayer1Id = awayMaleA?.PlayerId, AwayPlayer2Id = awayMaleB?.PlayerId
        });
        // Set 2: Men's Doubles 2
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 2, Phase = DropShot.Models.TeamMatchPhase.GenderDoubles,
            SetType = DropShot.Models.TeamMatchSetType.MensDoubles, CourtNumber = 1,
            HomePlayer1Id = homeMaleA?.PlayerId, HomePlayer2Id = homeMaleB?.PlayerId,
            AwayPlayer1Id = awayMaleA?.PlayerId, AwayPlayer2Id = awayMaleB?.PlayerId
        });
        // Set 3: Women's Doubles 1
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 3, Phase = DropShot.Models.TeamMatchPhase.GenderDoubles,
            SetType = DropShot.Models.TeamMatchSetType.WomensDoubles, CourtNumber = 2,
            HomePlayer1Id = homeFemaleA?.PlayerId, HomePlayer2Id = homeFemaleB?.PlayerId,
            AwayPlayer1Id = awayFemaleA?.PlayerId, AwayPlayer2Id = awayFemaleB?.PlayerId
        });
        // Set 4: Women's Doubles 2
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 4, Phase = DropShot.Models.TeamMatchPhase.GenderDoubles,
            SetType = DropShot.Models.TeamMatchSetType.WomensDoubles, CourtNumber = 2,
            HomePlayer1Id = homeFemaleA?.PlayerId, HomePlayer2Id = homeFemaleB?.PlayerId,
            AwayPlayer1Id = awayFemaleA?.PlayerId, AwayPlayer2Id = awayFemaleB?.PlayerId
        });

        // Phase 2: Mixed Doubles (Court 1 = A grade, Court 2 = B grade)
        // Set 5: Mixed Doubles A 1
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 5, Phase = DropShot.Models.TeamMatchPhase.MixedDoubles,
            SetType = DropShot.Models.TeamMatchSetType.MixedDoublesA, CourtNumber = 1,
            HomePlayer1Id = homeMaleA?.PlayerId, HomePlayer2Id = homeFemaleA?.PlayerId,
            AwayPlayer1Id = awayMaleA?.PlayerId, AwayPlayer2Id = awayFemaleA?.PlayerId
        });
        // Set 6: Mixed Doubles A 2
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 6, Phase = DropShot.Models.TeamMatchPhase.MixedDoubles,
            SetType = DropShot.Models.TeamMatchSetType.MixedDoublesA, CourtNumber = 1,
            HomePlayer1Id = homeMaleA?.PlayerId, HomePlayer2Id = homeFemaleA?.PlayerId,
            AwayPlayer1Id = awayMaleA?.PlayerId, AwayPlayer2Id = awayFemaleA?.PlayerId
        });
        // Set 7: Mixed Doubles B 1
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 7, Phase = DropShot.Models.TeamMatchPhase.MixedDoubles,
            SetType = DropShot.Models.TeamMatchSetType.MixedDoublesB, CourtNumber = 2,
            HomePlayer1Id = homeMaleB?.PlayerId, HomePlayer2Id = homeFemaleB?.PlayerId,
            AwayPlayer1Id = awayMaleB?.PlayerId, AwayPlayer2Id = awayFemaleB?.PlayerId
        });
        // Set 8: Mixed Doubles B 2
        fixture.TeamMatchSets.Add(new TeamMatchSet
        {
            SetNumber = 8, Phase = DropShot.Models.TeamMatchPhase.MixedDoubles,
            SetType = DropShot.Models.TeamMatchSetType.MixedDoublesB, CourtNumber = 2,
            HomePlayer1Id = homeMaleB?.PlayerId, HomePlayer2Id = homeFemaleB?.PlayerId,
            AwayPlayer1Id = awayMaleB?.PlayerId, AwayPlayer2Id = awayFemaleB?.PlayerId
        });
    }

    private static List<string> ValidateMixedTeamComposition(List<CompetitionParticipant> members)
    {
        var errors = new List<string>();
        if (members.Count != 4)
        {
            errors.Add($"Team must have exactly 4 players (has {members.Count}).");
            return errors;
        }

        var maleA = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Male && m.Grade == DropShot.Models.PlayerGrade.A);
        var femaleA = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Female && m.Grade == DropShot.Models.PlayerGrade.A);
        var maleB = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Male && m.Grade == DropShot.Models.PlayerGrade.B);
        var femaleB = members.Count(m => m.Player?.Sex == DropShot.Models.PlayerSex.Female && m.Grade == DropShot.Models.PlayerGrade.B);

        if (maleA != 1) errors.Add($"Team needs exactly 1 Male A player (has {maleA}).");
        if (femaleA != 1) errors.Add($"Team needs exactly 1 Female A player (has {femaleA}).");
        if (maleB != 1) errors.Add($"Team needs exactly 1 Male B player (has {maleB}).");
        if (femaleB != 1) errors.Add($"Team needs exactly 1 Female B player (has {femaleB}).");

        var ungradedOrUngendered = members.Where(m => m.Grade == null || m.Player?.Sex == null).ToList();
        if (ungradedOrUngendered.Count > 0)
            errors.Add($"{ungradedOrUngendered.Count} player(s) missing grade or gender assignment.");

        return errors;
    }
}
