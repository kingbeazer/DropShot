using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Generic, persisted Notification Centre — not messaging-specific. Any
/// feature can post a notification for a user via <see cref="CreateAsync"/>;
/// <see cref="NotificationType"/> plus the optional reference id/payload let
/// the recipient UI decide how to render and where to link.
/// </summary>
public interface INotificationService
{
    Task<NotificationDto> CreateAsync(
        string userId, NotificationType type, string title,
        string? body = null, string? linkUrl = null,
        int? referenceId = null, string? payloadJson = null,
        CancellationToken ct = default);

    Task<List<NotificationDto>> GetRecentAsync(int take = 20, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(int notificationId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
}
