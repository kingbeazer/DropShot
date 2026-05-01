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

    /// <summary>
    /// Players list with joined linked-account user name and club membership names.
    /// Backs the SuperAdmin Players page (phase 4 batch B move). Authorization is
    /// enforced at the page (web shim) and the API endpoint level.
    /// </summary>
    Task<List<PlayerWithClubsDto>> GetPlayersWithClubsAsync(CancellationToken ct = default);

    Task<PlayerDto> CreatePlayerAsync(CreatePlayerRequest request, CancellationToken ct = default);
    Task<PlayerDto> UpdatePlayerAsync(int playerId, UpdatePlayerRequest request, CancellationToken ct = default);
    Task DeletePlayerAsync(int playerId, CancellationToken ct = default);
}
