namespace DropShot.Models;

public class LeagueDivision
{
    public int LeagueDivisionId { get; set; }
    public int LeagueSeasonId { get; set; }
    public byte Rank { get; set; } // 1 = top division
    public string Name { get; set; } = "";
    public int CompetitionId { get; set; }

    public LeagueSeason Season { get; set; } = null!;
    public Competition Competition { get; set; } = null!;
}
