namespace DropShot.Models;

public class CompetitionDivision
{
    public int CompetitionDivisionId { get; set; }
    public int CompetitionId { get; set; }
    public byte Rank { get; set; } // 1 = top division
    public string Name { get; set; } = "";

    public Competition Competition { get; set; } = null!;
    public ICollection<CompetitionParticipant> Participants { get; set; } = [];
    public ICollection<CompetitionTeam> Teams { get; set; } = [];
}
