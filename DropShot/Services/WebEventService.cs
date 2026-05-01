using DropShot.Data;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IEventService"/>. Mirrors
/// <c>EventsController</c> read endpoints. Phase 3 seed: read surface only.
/// </summary>
public sealed class WebEventService(IDbContextFactory<MyDbContext> dbFactory) : IEventService
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
}
