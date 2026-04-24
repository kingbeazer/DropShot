namespace DropShot.Models;

public enum ParticipantStatus : byte
{
    Registered   = 1,  // Added by admin but player has not yet confirmed participation
    FullPlayer   = 2,  // Active full participant (was Confirmed — byte value unchanged)
    Withdrawn    = 3,
    Disqualified = 4,
    Substitute   = 5,  // Available as a substitute, not in a team
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
