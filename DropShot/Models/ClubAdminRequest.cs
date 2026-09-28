using DropShot.Data;

namespace DropShot.Models;

public enum ClubAdminRequestStatus : byte
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// A self-service request by a linked <see cref="ClubPlayer"/> to be granted
/// club-administrator access. Approval by a site Admin/SuperAdmin inserts a
/// <see cref="ClubAdministrator"/> row and grants the user the ClubAdmin role.
/// </summary>
public class ClubAdminRequest
{
    public int ClubAdminRequestId { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public ClubAdminRequestStatus Status { get; set; } = ClubAdminRequestStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public string? ResolvedByUserId { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }
}
