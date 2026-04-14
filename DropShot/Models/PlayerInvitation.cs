using DropShot.Data;

namespace DropShot.Models;

public class PlayerInvitation
{
    public int PlayerInvitationId { get; set; }
    public Guid Token { get; set; } = Guid.NewGuid();

    public int LightPlayerId { get; set; }
    public Player? LightPlayer { get; set; }

    public string CreatedByUserId { get; set; } = "";
    public ApplicationUser? CreatedByUser { get; set; }

    public string? SentToEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedByUserId { get; set; }
}
