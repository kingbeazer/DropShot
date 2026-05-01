using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IClubService"/>. Lifts the Clubs
/// section of <see cref="ApiService"/>; the god-class is intentionally
/// duplicated through phases 3–7 and deleted in phase 8.
/// </summary>
public sealed class HttpClubService(HttpClient http) : IClubService
{
    public async Task<List<ClubDto>> GetClubsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ClubDto>>("api/clubs", ct) ?? [];

    public Task<ClubDetailDto?> GetClubAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ClubDetailDto>($"api/clubs/{id}", ct);

    public async Task<ClubDto> CreateClubAsync(SaveClubRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/clubs", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClubDto>(cancellationToken: ct))!;
    }

    public async Task<ClubDto> UpdateClubAsync(int clubId, SaveClubRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"api/clubs/{clubId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClubDto>(cancellationToken: ct))!;
    }

    public async Task DeleteClubAsync(int clubId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/clubs/{clubId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<CourtDto> AddCourtAsync(int clubId, AddCourtRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"api/clubs/{clubId}/courts", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CourtDto>(cancellationToken: ct))!;
    }

    public async Task<CourtDto> UpdateCourtAsync(int clubId, int courtId, UpdateCourtRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"api/clubs/{clubId}/courts/{courtId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CourtDto>(cancellationToken: ct))!;
    }

    public async Task DeleteCourtAsync(int clubId, int courtId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/clubs/{clubId}/courts/{courtId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<UserClubLinksDto> GetMyClubLinksAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<UserClubLinksDto>("api/clubs/my-links", ct)
            ?? new UserClubLinksDto([], [], []);

    public async Task RequestClubLinkAsync(int clubId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/clubs/{clubId}/link-requests", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task CancelMyClubLinkRequestAsync(int clubId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/clubs/{clubId}/link-requests/mine", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<ClubLinkRequestDto>> GetPendingLinkRequestsForAdminAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ClubLinkRequestDto>>(
            "api/clubadmin/link-requests", ct) ?? [];

    public async Task ApproveLinkRequestAsync(int requestId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/clubadmin/link-requests/{requestId}/approve", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RejectLinkRequestAsync(int requestId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/clubadmin/link-requests/{requestId}/reject", null, ct);
        resp.EnsureSuccessStatusCode();
    }
}
