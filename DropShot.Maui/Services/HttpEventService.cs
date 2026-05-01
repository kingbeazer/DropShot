using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IEventService"/>. Lifts the Events
/// section of <see cref="ApiService"/>.
/// </summary>
public sealed class HttpEventService(HttpClient http) : IEventService
{
    public async Task<List<EventDto>> GetEventsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<EventDto>>("api/events", ct) ?? [];

    public Task<EventDetailDto?> GetEventAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<EventDetailDto>($"api/events/{id}", ct);
}
