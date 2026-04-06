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
public class EventsController(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EventDto>>> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        await using var db = dbFactory.CreateDbContext();
        var events = await db.Events
            .Include(e => e.HostClub)
            .Include(e => e.Competitions)
            .OrderBy(e => e.Name)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .Select(e => new EventDto(
                e.EventId, e.Name, e.Description,
                e.StartDate, e.EndDate,
                e.HostClubId, e.HostClub != null ? e.HostClub.Name : null,
                e.Competitions.Count))
            .ToListAsync();
        return events;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventDetailDto>> Get(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var e = await db.Events
            .Include(ev => ev.HostClub)
            .Include(ev => ev.Competitions).ThenInclude(c => c.HostClub)
            .Include(ev => ev.Competitions).ThenInclude(c => c.Rules)
            .FirstOrDefaultAsync(ev => ev.EventId == id);

        if (e is null) return NotFound();

        return new EventDetailDto(
            e.EventId, e.Name, e.Description,
            e.StartDate, e.EndDate,
            e.HostClubId, e.HostClub?.Name,
            e.Competitions.Select(c => new CompetitionDto(
                c.CompetitionID, c.CompetitionName,
                (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
                c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
                (DropShot.Shared.PlayerSex?)c.EligibleSex,
                c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
                c.EventId, e.Name)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EventDto>> Create([FromBody] SaveEventRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var ev = new Event
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            HostClubId = req.HostClubId
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ev.EventId },
            new EventDto(ev.EventId, ev.Name, ev.Description, ev.StartDate, ev.EndDate,
                ev.HostClubId, null, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EventDto>> Update(int id, [FromBody] SaveEventRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var ev = await db.Events.Include(e => e.HostClub).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();

        if (!await authzService.CanEditCompetitionAsync(User, ev.HostClubId))
            return Forbid();

        ev.Name = req.Name.Trim();
        ev.Description = req.Description?.Trim();
        ev.StartDate = req.StartDate;
        ev.EndDate = req.EndDate;
        ev.HostClubId = req.HostClubId;
        await db.SaveChangesAsync();

        return new EventDto(ev.EventId, ev.Name, ev.Description, ev.StartDate, ev.EndDate,
            ev.HostClubId, ev.HostClub?.Name, await db.Competition.CountAsync(c => c.EventId == id));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFound();
        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/competitions")]
    public async Task<ActionResult<List<CompetitionDto>>> BulkCreateCompetitions(
        int id, [FromBody] CreateEventCompetitionsRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFound();

        if (!await authzService.CanEditCompetitionAsync(User, ev.HostClubId))
            return Forbid();

        var created = new List<Competition>();
        foreach (var t in req.Competitions)
        {
            var comp = new Competition
            {
                CompetitionName = t.CompetitionName.Trim(),
                CompetitionFormat = (DropShot.Models.CompetitionFormat)t.CompetitionFormat,
                EligibleSex = (DropShot.Models.PlayerSex?)t.EligibleSex,
                MaxAge = t.MaxAge,
                MinAge = t.MinAge,
                EventId = id,
                HostClubId = ev.HostClubId,
                StartDate = ev.StartDate,
                EndDate = ev.EndDate
            };
            db.Competition.Add(comp);
            created.Add(comp);
        }
        await db.SaveChangesAsync();

        return created.Select(c => new CompetitionDto(
            c.CompetitionID, c.CompetitionName,
            (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
            c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
            (DropShot.Shared.PlayerSex?)c.EligibleSex,
            c.HostClubId, null, c.RulesSetId, null,
            c.EventId, ev.Name)).ToList();
    }
}
