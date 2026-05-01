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
/// Row in the My Players grid. Combines light players owned by the current
/// user and verified players the user has bookmarked. <c>HasMatchHistory</c>
/// is computed server-side so the page can hide the delete button on light
/// players whose matches need migrating first.
/// </summary>
public record MyPlayerRowDto(
    int PlayerId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    bool IsLight,
    string? Email,
    string? MobileNumber,
    PlayerSex? Sex,
    string? ProfileImagePath,
    bool HasMatchHistory);

public record CreateMyLightPlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    PlayerSex? Sex);

public record UpdateMyLightPlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    PlayerSex? Sex);

/// <summary>
/// Minimal projection of an <c>ApplicationUser</c>. Used by the SuperAdmin
/// Players page's "Link account" dropdown and (later) Admin/UserManagement.
/// </summary>
public record ApplicationUserDto(
    string Id,
    string? UserName,
    string? Email);

public record LinkPlayerAccountRequest(string? UserId);

/// <summary>
/// Suggested "similar" verified player surfaced while typing a display name.
/// Web ranks via <c>FuzzySearchService</c>; MAUI hits the same endpoint.
/// </summary>
public record SimilarPlayerDto(
    int PlayerId,
    string DisplayName,
    string? FirstName,
    string? LastName);

/// <summary>
/// Light-player invitation handle returned by
/// <c>IInvitationService.CreateOrReuseLightPlayerInvitationAsync</c>.
/// The full URL is built server-side so MAUI doesn't need to know the host.
/// </summary>
public record LightPlayerInvitationDto(
    Guid Token,
    string InviteUrl);

public record SendInvitationEmailRequest(string Email);

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
