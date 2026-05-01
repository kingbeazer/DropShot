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
