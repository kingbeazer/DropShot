namespace DropShot.Shared.Dtos;

public record PlayerDto(
    int PlayerId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirth,
    PlayerSex? Sex,
    string? ContactPreferences,
    string? ProfileImagePath,
    string? UserId,
    string? MobileNumber,
    bool IsLight = false,
    string? CreatedByUserId = null);

public record CreatePlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirth,
    PlayerSex? Sex,
    string? ContactPreferences,
    string? MobileNumber,
    bool IsLight = false);

public record UpdatePlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirth,
    PlayerSex? Sex,
    string? ContactPreferences,
    string? MobileNumber);

/// <summary>
/// Row in the ClubPlayers grid (per-club roster). IsClubOwned is true when the
/// player is a light placeholder created specifically for this club —
/// determines whether the row exposes the edit / cascade-delete affordances.
/// </summary>
public record ClubPlayerDto(
    int PlayerId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    PlayerSex? Sex,
    DateOnly? DateOfBirth,
    string? ProfileImagePath,
    bool IsLight,
    bool IsClubOwned);

public record CreateLightPlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    DateOnly? DateOfBirth,
    PlayerSex? Sex);

public record UpdateLightPlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    DateOnly? DateOfBirth,
    PlayerSex? Sex);

/// <summary>
/// Row in the SuperAdmin Players grid. Joins each player with the linked
/// ASP.NET account user-name (when present) and the names of the clubs they
/// belong to.
/// </summary>
public record PlayerWithClubsDto(
    int PlayerId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    DateOnly? DateOfBirth,
    PlayerSex? Sex,
    string? ContactPreferences,
    string? ProfileImagePath,
    string? UserId,
    string? LinkedUserName,
    bool IsLight,
    string? CreatedByUserId,
    IReadOnlyList<string> ClubNames);

/// <summary>
/// Row in the global league table aggregated from <c>SavedMatch</c>. Position is
/// implied by list order. PlayerName is resolved from current display name when
/// the saved match has a PlayerId; otherwise it falls back to the historical
/// name string captured at match time.
/// </summary>
public record GlobalLeagueTableEntryDto(
    string PlayerName,
    int Played,
    int Won,
    int Lost,
    int Points);
