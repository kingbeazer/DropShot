using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Club domain abstraction. Phase 4 batch B.4 covers the public Clubs page
/// surface: list, detail, club CRUD, court CRUD, and the user's link-request
/// flow. The 14 admin-template / rules-management write paths stay web-only
/// until phase 5 — they aren't exposed on this interface yet.
/// </summary>
public interface IClubService
{
    Task<List<ClubDto>> GetClubsAsync(CancellationToken ct = default);
    Task<ClubDetailDto?> GetClubAsync(int id, CancellationToken ct = default);

    Task<ClubDto> CreateClubAsync(SaveClubRequest request, CancellationToken ct = default);
    Task<ClubDto> UpdateClubAsync(int clubId, SaveClubRequest request, CancellationToken ct = default);
    Task DeleteClubAsync(int clubId, CancellationToken ct = default);

    Task<CourtDto> AddCourtAsync(int clubId, AddCourtRequest request, CancellationToken ct = default);
    Task<CourtDto> UpdateCourtAsync(int clubId, int courtId, UpdateCourtRequest request, CancellationToken ct = default);
    Task DeleteCourtAsync(int clubId, int courtId, CancellationToken ct = default);

    /// <summary>Caller's link state across all clubs (admin / linked / pending).</summary>
    Task<UserClubLinksDto> GetMyClubLinksAsync(CancellationToken ct = default);

    Task RequestClubLinkAsync(int clubId, CancellationToken ct = default);
    Task CancelMyClubLinkRequestAsync(int clubId, CancellationToken ct = default);
}
