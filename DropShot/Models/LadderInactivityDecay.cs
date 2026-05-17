namespace DropShot.Models;

/// <summary>
/// Audit row written by <c>LadderInactivityService</c> whenever a SinglesLadder
/// participant's rating is decayed for inactivity. One row per weekly decay
/// step; lets the recent-activity feed render the decay event alongside
/// completed fixtures.
/// </summary>
public class LadderInactivityDecay
{
    public int LadderInactivityDecayId { get; set; }
    public int CompetitionId { get; set; }
    public int PlayerId { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public double RatingBefore { get; set; }
    public double RatingAfter { get; set; }
    public int DaysInactive { get; set; }

    public Competition Competition { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
