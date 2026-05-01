using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Club domain abstraction. Seeded with the read surface needed by Clubs and
/// ClubPlayers (phase 4); admin and link-request methods land in phase 5.
/// </summary>
public interface IClubService
{
    Task<List<ClubDto>> GetClubsAsync(CancellationToken ct = default);
    Task<ClubDetailDto?> GetClubAsync(int id, CancellationToken ct = default);
}
