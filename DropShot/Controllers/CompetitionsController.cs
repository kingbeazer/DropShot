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
            .Include(x => x.Participants).ThenInclude(p => p.Team)
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
                p.RegisteredAt, p.TeamId, p.Team?.Name,
                p.Player?.MobileNumber)).ToList());
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
            .OrderBy(t => t.Name)
            .ToListAsync();
        return teams.Select(t => new CompetitionTeamDto(t.CompetitionTeamId, t.CompetitionId, t.Name)).ToList();
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
            .Include(c => c.Participants)
            .Include(c => c.MatchWindows)
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

        DateTime RandomSlot() =>
            SchedulingSlotPicker.PickSlot(matchWindows, startDate, endDate, rng);

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
                            db.CompetitionFixtures.Add(new CompetitionFixture
                            {
                                CompetitionId = id,
                                CompetitionStageId = stage.CompetitionStageId,
                                Player1Id = players[i],
                                Player2Id = players[j],
                                ScheduledAt = RandomSlot(),
                                Status = DropShot.Models.FixtureStatus.Scheduled
                            });
                        }
                    }
                    break;
                }

                case DropShot.Models.StageType.Knockout:
                {
                    // Smart knockout: generates exactly the right rounds based on player count.
                    // No Byes, no power-of-2 bracket inflation.
                    //
                    //  n >= 8  →  Quarter-Finals (top 8, 4 matches) + 2 SF placeholders + Final
                    //  n >= 4  →  Semi-Finals (top 4, 2 matches) + Final placeholder
                    //  n >= 2  →  Final only (2 players)
                    //
                    // Players are seeded from the RR league table when results exist,
                    // otherwise from registration order.

                    int n = activePlayers.Count;
                    if (n < 2) break;

                    if (n >= 8)
                    {
                        // ── Quarter-Finals (players assigned automatically when RR completes) ──
                        for (int m = 0; m < 4; m++)
                        {
                            db.CompetitionFixtures.Add(new CompetitionFixture
                            {
                                CompetitionId = id,
                                CompetitionStageId = stage.CompetitionStageId,
                                FixtureLabel = $"Quarter-Final {m + 1}",
                                RoundNumber = 1,
                                ScheduledAt = RandomSlot(),
                                Status = DropShot.Models.FixtureStatus.Scheduled
                            });
                        }

                        // ── Semi-Finals (players assigned automatically when QF completes) ───
                        for (int m = 0; m < 2; m++)
                        {
                            db.CompetitionFixtures.Add(new CompetitionFixture
                            {
                                CompetitionId = id,
                                CompetitionStageId = stage.CompetitionStageId,
                                FixtureLabel = $"Semi-Final {m + 1}",
                                RoundNumber = 2,
                                ScheduledAt = RandomSlot(),
                                Status = DropShot.Models.FixtureStatus.Scheduled
                            });
                        }
                    }
                    else
                    {
                        // ── Semi-Finals (players assigned automatically when RR completes) ────
                        for (int m = 0; m < 2; m++)
                        {
                            db.CompetitionFixtures.Add(new CompetitionFixture
                            {
                                CompetitionId = id,
                                CompetitionStageId = stage.CompetitionStageId,
                                FixtureLabel = $"Semi-Final {m + 1}",
                                RoundNumber = 1,
                                ScheduledAt = RandomSlot(),
                                Status = DropShot.Models.FixtureStatus.Scheduled
                            });
                        }
                    }

                    // ── Final (always a placeholder) ─────────────────────────────
                    db.CompetitionFixtures.Add(new CompetitionFixture
                    {
                        CompetitionId = id,
                        CompetitionStageId = stage.CompetitionStageId,
                        FixtureLabel = "Final",
                        RoundNumber = n >= 8 ? 3 : 2,
                        ScheduledAt = RandomSlot(),
                        Status = DropShot.Models.FixtureStatus.Scheduled
                    });
                    break;
                }

                case DropShot.Models.StageType.Final:
                {
                    db.CompetitionFixtures.Add(new CompetitionFixture
                    {
                        CompetitionId = id,
                        CompetitionStageId = stage.CompetitionStageId,
                        FixtureLabel = "Final",
                        RoundNumber = 1,
                        ScheduledAt = RandomSlot(),
                        Status = DropShot.Models.FixtureStatus.Scheduled
                    });
                    break;
                }

                case DropShot.Models.StageType.QuarterFinal:
                {
                    for (int m = 0; m < 4; m++)
                    {
                        db.CompetitionFixtures.Add(new CompetitionFixture
                        {
                            CompetitionId = id,
                            CompetitionStageId = stage.CompetitionStageId,
                            FixtureLabel = $"Quarter-Final {m + 1}",
                            RoundNumber = 1,
                            ScheduledAt = RandomSlot(),
                            Status = DropShot.Models.FixtureStatus.Scheduled
                        });
                    }
                    break;
                }

                case DropShot.Models.StageType.SemiFinal:
                {
                    for (int m = 0; m < 2; m++)
                    {
                        db.CompetitionFixtures.Add(new CompetitionFixture
                        {
                            CompetitionId = id,
                            CompetitionStageId = stage.CompetitionStageId,
                            FixtureLabel = $"Semi-Final {m + 1}",
                            RoundNumber = 1,
                            ScheduledAt = RandomSlot(),
                            Status = DropShot.Models.FixtureStatus.Scheduled
                        });
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

        var table = participants.ToDictionary(
            cp => cp.PlayerId,
            cp => new { Name = cp.Player?.DisplayName ?? "", Played = 0, Won = 0, Lost = 0 });

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
                if (pid == f.WinnerPlayerId) won[pid]++;
                else lost[pid]++;
            }
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
        c.EligibleSex = (DropShot.Models.PlayerSex?)r.EligibleSex;
        c.HostClubId = r.HostClubId;
        c.RulesSetId = r.RulesSetId;
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
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge,
        (DropShot.Shared.PlayerSex?)c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name);

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
        f.ResultSummary, f.WinnerPlayerId);
}
