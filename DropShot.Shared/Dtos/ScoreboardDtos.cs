namespace DropShot.Shared.Dtos;

/// <summary>
/// Court info needed by the scoreboard subcomponents (Default + Wimbledon
/// layouts). The web-only host page constructs these from EF entities;
/// any future RCL move of Scoreboard.razor will source them via the API.
/// </summary>
public record ScoreboardCourtDto(int CourtId, string ClubName, string CourtName);

/// <summary>
/// Active match info for scoreboard display: just the player labels (or
/// team-match home/away names) plus the start timestamp the Wimbledon
/// layout uses for the elapsed-time clock.
/// </summary>
public record ScoreboardActiveMatchDto(
    string? Player1,
    string? Player2,
    string? Player3,
    string? Player4,
    DateTime CreatedAt);

/// <summary>
/// Fixture metadata rendered alongside the scoreboard when an active
/// match is tied to a competition fixture (competition name + fixture label).
/// </summary>
public record ScoreboardFixtureDto(string? CompetitionName, string? FixtureLabel);
