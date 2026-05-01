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

    /// <summary>Roster for a specific club (joins ClubPlayer + Player + linked account image).</summary>
    Task<List<ClubPlayerDto>> GetClubPlayersAsync(int clubId, CancellationToken ct = default);

    /// <summary>Search non-light players who aren't already in the given club. Caps at 10 results.</summary>
    Task<List<PlayerDto>> SearchPlayersForClubLinkAsync(int clubId, string term, CancellationToken ct = default);

    /// <summary>Insert a light placeholder player for the club; returns the new player.</summary>
    Task<PlayerDto> CreateLightPlayerAsync(int clubId, CreateLightPlayerRequest request, CancellationToken ct = default);

    /// <summary>Update a light, club-owned player. Throws if the player is non-light or owned by another club.</summary>
    Task<PlayerDto> UpdateLightPlayerAsync(int clubId, int playerId, UpdateLightPlayerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Remove the player's membership from the club. If the player is light and
    /// club-owned (CreatedByClubId == clubId), the Player record is also deleted.
    /// </summary>
    Task RemovePlayerFromClubAsync(int clubId, int playerId, CancellationToken ct = default);

    /// <summary>Add an existing (non-light) player to the club's roster.</summary>
    Task LinkExistingPlayerToClubAsync(int clubId, int playerId, CancellationToken ct = default);

    // ── My Players (phase 5 — user-owned light players + bookmarked verified) ──

    /// <summary>Light players the current user created plus verified players they've bookmarked.</summary>
    Task<List<MyPlayerRowDto>> GetMyPlayersAsync(CancellationToken ct = default);

    Task<PlayerDto> CreateMyLightPlayerAsync(CreateMyLightPlayerRequest request, CancellationToken ct = default);

    /// <summary>Update a light player owned by the current user. Throws if not owned.</summary>
    Task<PlayerDto> UpdateMyLightPlayerAsync(int playerId, UpdateMyLightPlayerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a light player owned by the current user. Throws if the player has
    /// match history (callers should use <c>LinkLightToVerifiedAsync</c> first).
    /// </summary>
    Task DeleteMyLightPlayerAsync(int playerId, CancellationToken ct = default);

    /// <summary>
    /// Migrate match history from a user-owned light player to a verified player,
    /// delete the light record, and bookmark the verified player. The whole
    /// operation runs server-side so MAUI gets the same atomic semantics.
    /// </summary>
    Task LinkLightToVerifiedAsync(int lightPlayerId, int verifiedPlayerId, CancellationToken ct = default);

    /// <summary>Fuzzy-rank verified players by display-name similarity, capped to <paramref name="max"/>.</summary>
    Task<List<SimilarPlayerDto>> SearchSimilarVerifiedPlayersAsync(string term, int max, CancellationToken ct = default);
}
