using DropShot.Data;

namespace DropShot.Models;

public enum ClubLinkRequestStatus : byte
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// A self-service request by a <see cref="ApplicationUser"/> to be linked to a
/// <see cref="Club"/> as a player. Approval by a club admin creates a
/// <see cref="Player"/> if the user doesn't already have one, then inserts a
/// <see cref="ClubPlayer"/> row.
/// </summary>
public class ClubLinkRequest
{
    public int ClubLinkRequestId { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public ClubLinkRequestStatus Status { get; set; } = ClubLinkRequestStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public string? ResolvedByUserId { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }
}
