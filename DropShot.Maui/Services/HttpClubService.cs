using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IClubService"/>. Lifts the Clubs
/// section of <see cref="ApiService"/>.
/// </summary>
public sealed class HttpClubService(HttpClient http) : IClubService
{
    public async Task<List<ClubDto>> GetClubsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ClubDto>>("api/clubs", ct) ?? [];

    public Task<ClubDetailDto?> GetClubAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ClubDetailDto>($"api/clubs/{id}", ct);
}
