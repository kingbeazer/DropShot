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
}
