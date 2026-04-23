namespace DropShot.Models;

public class SavedMatch
{
    public int SavedMatchId { get; set; }
    public string? MatchJson { get; set; }
    public bool Complete { get; set; }
    public string? Player1 { get; set; }
    public string? Player2 { get; set; }
    public string? Player3 { get; set; }
    public string? Player4 { get; set; }
    public int? Player1Id { get; set; }
    public int? Player2Id { get; set; }
    public int? Player3Id { get; set; }
    public int? Player4Id { get; set; }
    public string? WinnerName { get; set; }
    public int? WinnerPlayerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int? CourtId { get; set; }
    public string? UserId { get; set; }
    public string? DeviceToken { get; set; }

    // Updated on every score write. Used to spot abandoned matches so a
    // new user can claim the court if the previous scorer disappeared.
    public DateTime? LastActivityAt { get; set; }

    // When the current scorer has confirmed "still playing" in response
    // to a court challenge, other users are blocked until this timestamp.
    public DateTime? ClaimGraceUntilUtc { get; set; }
}
