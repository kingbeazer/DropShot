using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Event domain abstraction. Seeded with the read surface needed by ViewEvent
/// (phase 4); CreateEventDialog write methods land in phase 5.
/// </summary>
public interface IEventService
{
    Task<List<EventDto>> GetEventsAsync(CancellationToken ct = default);
    Task<EventDetailDto?> GetEventAsync(int id, CancellationToken ct = default);

    /// <summary>Create a new event (admin / club admin only — server enforces).</summary>
    Task<EventDto> CreateEventAsync(SaveEventRequest request, CancellationToken ct = default);

    /// <summary>
    /// Bulk-create competitions under an existing event from the
    /// CreateEventDialog payload. Returns the created competitions.
    /// </summary>
    Task<List<CompetitionDto>> BulkCreateCompetitionsAsync(
        int eventId, CreateEventCompetitionsRequest request, CancellationToken ct = default);
}
