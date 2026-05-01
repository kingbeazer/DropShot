using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Scoreboard domain abstraction. Phase 6 covers Scoreboard.razor: the courts
/// list (admin-scoped), per-court state snapshot (active match, fixture,
/// current score derived from MatchJson), and the per-court display settings
/// (layout / fullscreen / live-stream URL). Live updates flow through SignalR
/// via <see cref="IScoreboardHubFactory"/>; this interface covers the REST
/// snapshot reads + display-setting writes.
/// </summary>
public interface IScoreboardService
{
    /// <summary>
    /// Courts the caller can administer. Admin / SuperAdmin see every court;
    /// ClubAdmin sees courts of their admin clubs.
    /// </summary>
    Task<List<ScoreboardCourtDto>> GetAdminCourtsAsync(CancellationToken ct = default);

    /// <summary>
    /// Snapshot of the live state on a court: the in-progress match, its
    /// linked competition fixture, the most recent <c>GameState</c> (parsed
    /// from MatchJson), and the persisted per-court display settings.
    /// </summary>
    Task<ScoreboardCourtStateDto> GetCourtStateAsync(int courtId, CancellationToken ct = default);

    /// <summary>Update one or more display settings on a court (layout / fullscreen / live-stream).</summary>
    Task UpdateDisplaySettingAsync(int courtId, UpdateDisplaySettingRequest request, CancellationToken ct = default);
}
