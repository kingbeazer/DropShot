using DropShot.Data;
using DropShot.Hubs;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public sealed class WebNotificationService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser,
    IHubContext<MessagingHub> hubContext) : INotificationService
{
    public async Task<NotificationDto> CreateAsync(
        string userId, NotificationType type, string title,
        string? body = null, string? linkUrl = null,
        int? referenceId = null, string? payloadJson = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            LinkUrl = linkUrl,
            ReferenceId = referenceId,
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        var unreadCount = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
        var dto = ToDto(notification);
        await hubContext.Clients.User(userId).SendAsync("NotificationReceived", dto, unreadCount, ct);

        return dto;
    }

    public async Task<List<NotificationDto>> GetRecentAsync(int take = 20, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var safeTake = Math.Clamp(take, 1, 100);

        var notifications = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(safeTake)
            .ToListAsync(ct);

        return notifications.Select(ToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return 0;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId, ct);
        if (notification is null || notification.ReadAt is not null) return;

        notification.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(ct);
        if (unread.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var n in unread) n.ReadAt = now;
        await db.SaveChangesAsync(ct);
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.NotificationId, n.Type, n.Title, n.Body, n.LinkUrl, n.ReferenceId, n.CreatedAt, n.ReadAt);
}
