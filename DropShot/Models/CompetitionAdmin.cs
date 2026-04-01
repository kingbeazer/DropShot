using DropShot.Data;

namespace DropShot.Models;

public class CompetitionAdmin
{
    public int CompetitionId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Competition Competition { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
