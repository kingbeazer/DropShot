namespace DropShot.Models;

public class CompetitionTeam
{
    public int CompetitionTeamId { get; set; }
    public int CompetitionId { get; set; }
    public string Name { get; set; } = "";
    public int? CaptainPlayerId { get; set; }
    public int? CompetitionDivisionId { get; set; }

    public Competition Competition { get; set; } = null!;
    public Player? Captain { get; set; }
    public CompetitionDivision? Division { get; set; }
    public ICollection<CompetitionParticipant> Participants { get; set; } = [];
}
