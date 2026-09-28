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

    // ── Club admin moderation (phase 5 — backs ClubAdmin/ClubLinkRequests) ──

    /// <summary>
    /// Pending link requests across every club the caller administers. Admin
    /// and SuperAdmin see all pending requests; ClubAdmin sees just their
    /// admin clubs' requests.
    /// </summary>
    Task<List<ClubLinkRequestDto>> GetPendingLinkRequestsForAdminAsync(CancellationToken ct = default);

    /// <summary>
    /// Approve a link request: ensures a verified Player record exists for the
    /// requesting user, links them to the club via ClubPlayer, marks the
    /// request approved, and notifies the requester by email.
    /// </summary>
    Task ApproveLinkRequestAsync(int requestId, CancellationToken ct = default);

    /// <summary>Reject a link request and notify the requester by email.</summary>
    Task RejectLinkRequestAsync(int requestId, CancellationToken ct = default);

    // ── Club admin role requests (site admin approval only) ───────────────────

    /// <summary>
    /// Pending admin role requests. Only Admin/SuperAdmin can see these.
    /// </summary>
    Task<List<ClubAdminRequestDto>> GetPendingAdminRequestsAsync(CancellationToken ct = default);

    /// <summary>
    /// Submit a request for the caller to become a club administrator.
    /// Caller must already be linked to the club as a player.
    /// </summary>
    Task RequestClubAdminAsync(int clubId, CancellationToken ct = default);

    /// <summary>Cancel the caller's pending admin role request for a club.</summary>
    Task CancelMyClubAdminRequestAsync(int clubId, CancellationToken ct = default);

    /// <summary>
    /// Approve an admin role request: inserts ClubAdministrator, grants the
    /// ClubAdmin role, marks the request approved, and notifies the requester.
    /// Site Admin/SuperAdmin only.
    /// </summary>
    Task ApproveAdminRequestAsync(int requestId, CancellationToken ct = default);

    /// <summary>Reject an admin role request and notify the requester.</summary>
    Task RejectAdminRequestAsync(int requestId, CancellationToken ct = default);
}
