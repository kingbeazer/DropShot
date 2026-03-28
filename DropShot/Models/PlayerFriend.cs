namespace DropShot.Models;

public enum FriendStatus : byte
{
    Pending = 1,
    Accepted = 2,
    Blocked = 3
}

public class PlayerFriend
{
    public int PlayerId { get; set; }
    public int FriendPlayerId { get; set; }
    public FriendStatus Status { get; set; } = FriendStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Player Player { get; set; } = null!;
    public Player Friend { get; set; } = null!;
}
