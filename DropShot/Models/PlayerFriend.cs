using DropShot.Shared;

namespace DropShot.Models;

public class PlayerFriend
{
    public int PlayerId { get; set; }
    public int FriendPlayerId { get; set; }
    public FriendStatus Status { get; set; } = FriendStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Player Player { get; set; } = null!;
    public Player Friend { get; set; } = null!;
}
