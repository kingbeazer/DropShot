namespace DropShot.Models;

public enum TeamMatchPhase : byte
{
    GenderDoubles = 1,
    MixedDoubles = 2
}

public enum TeamMatchSetType : byte
{
    MensDoubles = 1,
    WomensDoubles = 2,
    MixedDoublesA = 3,
    MixedDoublesB = 4
}

public class TeamMatchSet
{
    public int TeamMatchSetId { get; set; }
    public int CompetitionFixtureId { get; set; }
    public int SetNumber { get; set; }
    public TeamMatchPhase Phase { get; set; }
    public TeamMatchSetType SetType { get; set; }
    public int CourtNumber { get; set; } // 1 or 2

    // Players for this specific set (doubles pair per side)
    public int? HomePlayer1Id { get; set; }
    public int? HomePlayer2Id { get; set; }
    public int? AwayPlayer1Id { get; set; }
    public int? AwayPlayer2Id { get; set; }

    // Set score (games won)
    public int HomeGames { get; set; }
    public int AwayGames { get; set; }
    public int? WinnerTeamId { get; set; }
    public bool IsComplete { get; set; }

    // Link to live scoring system
    public int? SavedMatchId { get; set; }

    // Navigation
    public CompetitionFixture CompetitionFixture { get; set; } = null!;
    public Player? HomePlayer1 { get; set; }
    public Player? HomePlayer2 { get; set; }
    public Player? AwayPlayer1 { get; set; }
    public Player? AwayPlayer2 { get; set; }
    public CompetitionTeam? WinnerTeam { get; set; }
    public SavedMatch? SavedMatch { get; set; }
}
