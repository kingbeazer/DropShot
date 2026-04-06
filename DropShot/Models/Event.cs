namespace DropShot.Models;

public class Event
{
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? HostClubId { get; set; }

    public Club? HostClub { get; set; }
    public ICollection<Competition> Competitions { get; set; } = [];
}
