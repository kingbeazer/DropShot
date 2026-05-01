namespace DropShot.Shared.Dtos;

/// <summary>
/// Row in the Match landing page's "Active Matches" list. Backs the
/// non-admin "if they have an active match, go straight to it" shortcut and
/// the admin's match-list rendering.
/// </summary>
public record ActiveMatchDto(
    int SavedMatchId,
    string? Player1,
    string? Player2,
    string? Player3,
    string? Player4,
    int? CourtId,
    string? CourtName,
    DateTime CreatedAt);

/// <summary>
/// One set's game count for a casual match. May represent the only
/// set when the match was scored in game-only mode (no per-set
/// breakdown was recorded), in which case <see cref="SetNumber"/> == 1.
/// </summary>
public record CasualSetScoreDto(int SetNumber, int Player1Games, int Player2Games);

/// <summary>
/// Recently completed casual match (a SavedMatch row not linked to a
/// CompetitionFixture). Backs Home.razor's "My Recent Results" list.
/// Sets are pre-parsed from MatchJson server-side so the shared RCL
/// never deserializes the Match graph.
/// </summary>
public record RecentCasualMatchDto(
    int SavedMatchId,
    string? Player1,
    string? Player2,
    string? Player3,
    string? Player4,
    int? Player1Id,
    int? Player2Id,
    int? Player3Id,
    int? Player4Id,
    string? WinnerName,
    int? WinnerPlayerId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<CasualSetScoreDto> Sets);
