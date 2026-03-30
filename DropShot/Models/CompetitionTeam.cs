namespace DropShot.Models;

public class CompetitionTeam
{
    public int CompetitionTeamId { get; set; }
    public int CompetitionId { get; set; }
    public string Name { get; set; } = "";

    public Competition Competition { get; set; } = null!;
    public ICollection<CompetitionParticipant> Participants { get; set; } = [];
}
