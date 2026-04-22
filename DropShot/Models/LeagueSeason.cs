namespace DropShot.Models;

public enum LeagueSeasonStatus : byte
{
    Planning = 1,
    Active = 2,
    Closed = 3,
}

public class LeagueSeason
{
    public int LeagueSeasonId { get; set; }
    public int LeagueId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public LeagueSeasonStatus Status { get; set; } = LeagueSeasonStatus.Planning;
    public DateTime? ClosedAt { get; set; }

    public League League { get; set; } = null!;
    public ICollection<LeagueDivision> Divisions { get; set; } = [];
}
