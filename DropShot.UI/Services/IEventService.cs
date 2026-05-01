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
}
