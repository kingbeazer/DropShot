using DropShot.Data;

namespace DropShot.Models;

public class ClubAdministrator
{
    public string UserId { get; set; } = "";
    public int ClubId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public Club Club { get; set; } = null!;
}
