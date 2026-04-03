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
