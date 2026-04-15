namespace DropShot.Models;

public class ClubPlayer
{
    public int ClubId { get; set; }
    public int PlayerId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public Club Club { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
