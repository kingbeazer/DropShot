namespace DropShot.Models;

public enum FixtureStatus : byte
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Walkover = 5,
    AwaitingVerification = 6
}

public class CompetitionFixture
{
    public int CompetitionFixtureId { get; set; }
    public int CompetitionId { get; set; }
    public int? CompetitionStageId { get; set; }
    public int? CourtId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public FixtureStatus Status { get; set; } = FixtureStatus.Scheduled;

    public int? Player1Id { get; set; }
    public int? Player2Id { get; set; }
    public int? Player3Id { get; set; }
    public int? Player4Id { get; set; }

    public string? FixtureLabel { get; set; }
    public int? RoundNumber { get; set; }

    public int? SavedMatchId { get; set; }
    public string? ResultSummary { get; set; }
    public int? WinnerPlayerId { get; set; }
    public Guid? VerificationToken { get; set; }

    // Audit trail for admin-modified results
    public string? OriginalResultSummary { get; set; }
    public int? OriginalWinnerPlayerId { get; set; }
    public bool ResultModifiedByAdmin { get; set; }

    // Mixed Team Tennis fields
    public int? HomeTeamId { get; set; }
    public int? AwayTeamId { get; set; }
    public int? WinnerTeamId { get; set; }
    public int? CourtPairId { get; set; }

    // Per-fixture score aggregates. Populated when a match completes so the
    // league-table endpoint can honour Competition.LeagueScoring without
    // re-parsing ResultSummary or joining SavedMatch.MatchJson. For singles/doubles
    // the "home" side is Player1/Player3 and "away" is Player2/Player4; for team
    // matches these mirror HomeTeam/AwayTeam. Null for legacy fixtures that
    // completed before this column existed.
    public int? HomeSetsWon { get; set; }
    public int? AwaySetsWon { get; set; }
    public int? HomeGamesTotal { get; set; }
    public int? AwayGamesTotal { get; set; }

    public Competition Competition { get; set; } = null!;
    public CompetitionStage? Stage { get; set; }
    public Court? Court { get; set; }
    public Player? Player1 { get; set; }
    public Player? Player2 { get; set; }
    public Player? Player3 { get; set; }
    public Player? Player4 { get; set; }
    public SavedMatch? SavedMatch { get; set; }
    public CompetitionTeam? HomeTeam { get; set; }
    public CompetitionTeam? AwayTeam { get; set; }
    public CompetitionTeam? WinnerTeam { get; set; }
    public CourtPair? CourtPair { get; set; }
    public ICollection<Rubber> Rubbers { get; set; } = [];
}
