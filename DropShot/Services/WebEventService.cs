using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IEventService"/>. Mirrors
/// <c>EventsController</c> read + write endpoints.
/// </summary>
public sealed class WebEventService(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    IHttpContextAccessor httpContextAccessor) : IEventService
{
    public async Task<List<EventDto>> GetEventsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Events
            .Include(e => e.HostClub)
            .Include(e => e.Competitions)
            .OrderBy(e => e.Name)
            .Select(e => new EventDto(
                e.EventId, e.Name, e.Description,
                e.StartDate, e.EndDate,
                e.HostClubId, e.HostClub != null ? e.HostClub.Name : null,
                e.Competitions.Count))
            .ToListAsync(ct);
    }

    public async Task<EventDetailDto?> GetEventAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var e = await db.Events
            .Include(ev => ev.HostClub)
            .Include(ev => ev.Competitions).ThenInclude(c => c.HostClub)
            .Include(ev => ev.Competitions).ThenInclude(c => c.Rules)
            .FirstOrDefaultAsync(ev => ev.EventId == id, ct);

        if (e is null) return null;

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

    public async Task<EventDto> CreateEventAsync(SaveEventRequest request, CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        if (!await authzService.CanEditCompetitionAsync(user, request.HostClubId))
            throw new UnauthorizedAccessException("You can't create events for this club.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ev = new Event
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            HostClubId = request.HostClubId
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        var hostClubName = ev.HostClubId.HasValue
            ? await db.Clubs.Where(c => c.ClubId == ev.HostClubId.Value).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : null;
        return new EventDto(ev.EventId, ev.Name, ev.Description, ev.StartDate, ev.EndDate,
            ev.HostClubId, hostClubName, 0);
    }

    public async Task<List<CompetitionDto>> BulkCreateCompetitionsAsync(
        int eventId, CreateEventCompetitionsRequest request, CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ev = await db.Events.FindAsync(new object?[] { eventId }, ct)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");
        if (!await authzService.CanEditCompetitionAsync(user, ev.HostClubId))
            throw new UnauthorizedAccessException("You can't add competitions to this event.");

        var created = new List<Competition>();
        foreach (var t in request.Competitions)
        {
            var comp = new Competition
            {
                CompetitionName = t.CompetitionName.Trim(),
                CompetitionFormat = (DropShot.Models.CompetitionFormat)t.CompetitionFormat,
                EligibleSex = (DropShot.Models.PlayerSex?)t.EligibleSex,
                MaxAge = t.MaxAge,
                MinAge = t.MinAge,
                EventId = eventId,
                HostClubId = ev.HostClubId,
                StartDate = ev.StartDate,
                EndDate = ev.EndDate
            };
            db.Competition.Add(comp);
            created.Add(comp);
        }
        await db.SaveChangesAsync(ct);

        return created.Select(c => new CompetitionDto(
            c.CompetitionID, c.CompetitionName,
            (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
            c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
            (DropShot.Shared.PlayerSex?)c.EligibleSex,
            c.HostClubId, null, c.RulesSetId, null,
            c.EventId, ev.Name)).ToList();
    }
}
