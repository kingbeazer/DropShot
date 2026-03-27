using DropShot.Data;

namespace DropShot.Models;

public class Player
{
    public int PlayerId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
