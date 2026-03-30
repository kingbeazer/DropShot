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
    string? MobileNumber);

public record CreatePlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirth,
    PlayerSex? Sex,
    string? ContactPreferences,
    string? MobileNumber);

public record UpdatePlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirth,
    PlayerSex? Sex,
    string? ContactPreferences,
    string? MobileNumber);
