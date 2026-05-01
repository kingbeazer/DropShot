using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Player domain abstraction. Methods are added incrementally as pages migrate
/// into <c>DropShot.UI</c> (phase 4 onward); this seed covers the read surface
/// needed by Players, ClubPlayers, and LeagueTable.
/// </summary>
public interface IPlayerService
{
    Task<List<PlayerDto>> GetPlayersAsync(CancellationToken ct = default);
    Task<PlayerDto?> GetPlayerAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Global cross-club league table aggregated from <c>SavedMatch</c>.
    /// Used by the LeagueTable page (phase 4 move).
    /// </summary>
    Task<List<GlobalLeagueTableEntryDto>> GetGlobalLeagueTableAsync(CancellationToken ct = default);
}
