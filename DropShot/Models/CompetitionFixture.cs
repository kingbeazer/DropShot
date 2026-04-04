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

    public Competition Competition { get; set; } = null!;
    public CompetitionStage? Stage { get; set; }
    public Court? Court { get; set; }
    public Player? Player1 { get; set; }
    public Player? Player2 { get; set; }
    public Player? Player3 { get; set; }
    public Player? Player4 { get; set; }
    public SavedMatch? SavedMatch { get; set; }
}
