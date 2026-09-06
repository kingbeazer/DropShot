namespace DropShot.Shared.Dtos;

public record NotificationDto(
    int NotificationId,
    NotificationType Type,
    string Title,
    string? Body,
    string? LinkUrl,
    int? ReferenceId,
    DateTime CreatedAt,
    DateTime? ReadAt);
