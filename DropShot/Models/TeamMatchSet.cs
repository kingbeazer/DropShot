namespace DropShot.Models;

public class TeamMatchSet
{
    public int TeamMatchSetId { get; set; }
    public int CompetitionFixtureId { get; set; }
    public int SetNumber { get; set; }
    public TeamMatchPhase Phase { get; set; }
    public TeamMatchSetType SetType { get; set; }
    public int CourtNumber { get; set; } // 1 or 2

    public int? HomePlayer1Id { get; set; }
    public int? HomePlayer2Id { get; set; }
    public int? AwayPlayer1Id { get; set; }
    public int? AwayPlayer2Id { get; set; }

    public int? HomeGames { get; set; }
    public int? AwayGames { get; set; }
    public int? WinnerTeamId { get; set; }
    public bool IsComplete { get; set; }
    public int? SavedMatchId { get; set; }

    public CompetitionFixture Fixture { get; set; } = null!;
    public Player? HomePlayer1 { get; set; }
    public Player? HomePlayer2 { get; set; }
    public Player? AwayPlayer1 { get; set; }
    public Player? AwayPlayer2 { get; set; }
    public CompetitionTeam? WinnerTeam { get; set; }
    public SavedMatch? SavedMatch { get; set; }
}
