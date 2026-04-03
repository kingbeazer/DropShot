using DropShot.Data;

namespace DropShot.Models;

/// <summary>
/// Join table representing a user's personal player list ("My Players").
/// Light players are tracked via Player.CreatedByUserId; this table tracks
/// verified/full players that a user has added to their list.
/// </summary>
public class UserPlayer
{
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
