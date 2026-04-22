namespace DropShot.Models;

public enum ParticipantStatus : byte
{
    Registered = 1,
    Confirmed = 2,
    Withdrawn = 3,
    Disqualified = 4
}

public class CompetitionParticipant
{
    public int CompetitionId { get; set; }
    public int PlayerId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Registered;
    public int? TeamId { get; set; }
    public int? CompetitionDivisionId { get; set; }
    public string? Role { get; set; }

    public Competition Competition { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public CompetitionTeam? Team { get; set; }
    public CompetitionDivision? Division { get; set; }
}
