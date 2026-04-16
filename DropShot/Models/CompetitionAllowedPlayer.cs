namespace DropShot.Models;

/// <summary>
/// Explicit allow-list entry for a restricted <see cref="Competition"/>. When
/// <see cref="Competition.IsRestricted"/> is true, only players present in this
/// collection can see or enter the competition (in addition to the implicit
/// access rule for club or creator).
/// </summary>
public class CompetitionAllowedPlayer
{
    public int CompetitionId { get; set; }
    public int PlayerId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Competition Competition { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
