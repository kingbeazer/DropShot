using DropShot.Shared;

namespace DropShot.Models;

public class PlayerRatingSnapshot
{
    public int PlayerRatingSnapshotId { get; set; }
    public int PlayerId { get; set; }
    public int CompetitionId { get; set; }
    public PlayerRatingSnapshotKind Kind { get; set; }
    public double Rating { get; set; }
    public int RubbersPlayed { get; set; }
    public bool IsProvisional { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    public string? AcceptedByUserId { get; set; }

    public Player Player { get; set; } = null!;
    public Competition Competition { get; set; } = null!;
}
