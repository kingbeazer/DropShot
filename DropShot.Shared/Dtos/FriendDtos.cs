namespace DropShot.Shared.Dtos;

public record FriendDto(
    int PlayerId,
    string DisplayName,
    string? ProfileImagePath,
    string? UserId);

public record FriendRequestDto(
    int PlayerId,
    string DisplayName,
    string? ProfileImagePath,
    DateTime RequestedAt);

public record FriendSearchResultDto(
    int PlayerId,
    string DisplayName,
    string? ProfileImagePath,
    string? UserId,
    bool IsFriend,
    bool RequestPending);
