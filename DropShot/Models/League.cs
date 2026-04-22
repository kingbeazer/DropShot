namespace DropShot.Models;

public class League
{
    public int LeagueId { get; set; }
    public string Name { get; set; } = "";
    public int HostClubId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    // Format defaults copied to each season's underlying competitions
    public CompetitionFormat CompetitionFormat { get; set; } = CompetitionFormat.TeamMatch;
    public int TeamSize { get; set; } = 4;
    public LeagueScoringMode LeagueScoring { get; set; } = LeagueScoringMode.WinPoints;
    public string? RubberTemplateKey { get; set; }
    public MatchFormatType MatchFormat { get; set; } = MatchFormatType.BestOf;
    public int NumberOfSets { get; set; } = 3;
    public int GamesPerSet { get; set; } = 6;
    public SetWinMode SetWinMode { get; set; } = SetWinMode.WinBy2;

    // Division sizing
    public int TeamsPerDivisionTarget { get; set; } = 8;
    public int TeamsPerDivisionMin { get; set; } = 6;

    public Club HostClub { get; set; } = null!;
    public ICollection<LeagueSeason> Seasons { get; set; } = [];
    public ICollection<LeagueMembership> Memberships { get; set; } = [];
}
