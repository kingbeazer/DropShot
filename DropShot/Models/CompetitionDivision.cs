namespace DropShot.Models;

public class CompetitionDivision
{
    public int CompetitionDivisionId { get; set; }
    public int CompetitionId { get; set; }
    public byte Rank { get; set; } // 1 = top division
    public string Name { get; set; } = "";

    /// <summary>
    /// When true, this division uses the competition's shared
    /// (CompetitionDivisionId-null) match windows for auto-scheduling. When
    /// false, the user is expected to define division-scoped windows; if none
    /// exist, the setup page warns and the scheduler skips the division.
    /// </summary>
    public bool UseSharedMatchWindows { get; set; } = true;

    public Competition Competition { get; set; } = null!;
    public ICollection<CompetitionParticipant> Participants { get; set; } = [];
    public ICollection<CompetitionTeam> Teams { get; set; } = [];
}
