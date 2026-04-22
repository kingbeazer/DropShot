namespace DropShot.Models;

public class LeagueMembership
{
    public int LeagueId { get; set; }
    public int PlayerId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Last rank the player finished at (1 = top division). Used to seed the next
    // season's division buckets; null for a player who hasn't yet completed a season.
    public byte? CurrentDivisionRank { get; set; }

    public League League { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
