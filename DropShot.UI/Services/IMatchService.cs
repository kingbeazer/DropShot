using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Match-scoring domain abstraction. Phase 5 covers the Match landing page;
/// TeamMatchScoring + scoring-write endpoints land in phase 6.
/// </summary>
public interface IMatchService
{
    /// <summary>
    /// Active (incomplete) matches the caller can see. Logged-in users get
    /// matches scoped to their UserId; anonymous callers pass their device
    /// token to scope to matches started on their device.
    /// </summary>
    Task<List<ActiveMatchDto>> GetMyActiveMatchesAsync(string? deviceToken, CancellationToken ct = default);

    /// <summary>
    /// Recently completed casual matches the current user participated in.
    /// Returns an empty list when no user is signed in or no Player profile
    /// maps to the current UserId. Excludes SavedMatch rows linked to a
    /// CompetitionFixture (those render as fixture results, not casual).
    /// </summary>
    Task<List<RecentCasualMatchDto>> GetMyRecentCasualMatchesAsync(
        int limit = 6, CancellationToken ct = default);
}
