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

public sealed class WebMessagingService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser,
    IFriendService friendService,
    INotificationService notificationService,
    IHubContext<MessagingHub> hubContext) : IMessagingService
{
    private const int CooldownDays = 30;
    private const int PreviewLength = 80;

    private static (string userAId, string userBId) OrderPair(string x, string y) =>
        string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);

    private static string Preview(string body) =>
        body.Length > PreviewLength ? body[..PreviewLength] + "…" : body;

    public async Task<List<ConversationSummaryDto>> GetActiveConversationsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Conversations
            .Where(c => c.Status == ConversationStatus.Active && (c.UserAId == userId || c.UserBId == userId))
            .Include(c => c.UserA)
            .Include(c => c.UserB)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var conversationIds = rows.Select(r => r.ConversationId).ToList();
        var stats = await db.Messages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                LastBody = g.OrderByDescending(m => m.SentAt).Select(m => m.Body).First(),
                UnreadCount = g.Count(m => m.SenderUserId != userId && m.ReadAt == null)
            })
            .ToListAsync(ct);

        return rows.Select(c =>
        {
            var other = c.UserAId == userId ? c.UserB : c.UserA;
            var stat = stats.FirstOrDefault(s => s.ConversationId == c.ConversationId);
            return new ConversationSummaryDto(
                c.ConversationId, other.Id, other.DisplayName, other.ProfileImagePath,
                c.Status, stat is null ? null : Preview(stat.LastBody), c.LastMessageAt,
                c.RequestedByUserId == userId, stat?.UnreadCount ?? 0);
        }).ToList();
    }

    public async Task<List<ConversationSummaryDto>> GetIncomingMessageRequestsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Conversations
            .Where(c => c.Status == ConversationStatus.PendingApproval
                && c.RequestedByUserId != userId
                && (c.UserAId == userId || c.UserBId == userId))
            .Include(c => c.UserA)
            .Include(c => c.UserB)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(c =>
        {
            var other = c.UserAId == userId ? c.UserB : c.UserA;
            return new ConversationSummaryDto(
                c.ConversationId, other.Id, other.DisplayName, other.ProfileImagePath,
                c.Status, null, c.CreatedAt, false, 0);
        }).ToList();
    }

    public async Task<ConversationDetailDto?> GetConversationAsync(int conversationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return null;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Conversations
            .Include(x => x.UserA)
            .Include(x => x.UserB)
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId && (x.UserAId == userId || x.UserBId == userId), ct);
        if (c is null) return null;

        var other = c.UserAId == userId ? c.UserB : c.UserA;
        return new ConversationDetailDto(c.ConversationId, other.Id, other.DisplayName, other.ProfileImagePath,
            c.Status, c.RequestedByUserId == userId);
    }

    public async Task<List<MessageDto>> GetMessagesAsync(int conversationId, int? beforeMessageId, int pageSize, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId && (c.UserAId == userId || c.UserBId == userId), ct);
        if (conversation is null) return [];
        if (conversation.Status == ConversationStatus.PendingApproval && conversation.RequestedByUserId != userId)
            return [];

        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Messages.Where(m => m.ConversationId == conversationId);
        if (beforeMessageId is not null)
            query = query.Where(m => m.MessageId < beforeMessageId.Value);

        var messages = await query
            .OrderByDescending(m => m.MessageId)
            .Take(safePageSize)
            .OrderBy(m => m.MessageId)
            .ToListAsync(ct);

        return messages.Select(m => new MessageDto(m.MessageId, m.ConversationId, m.SenderUserId, m.Body, m.SentAt, m.SenderUserId == userId)).ToList();
    }

    public async Task<SendMessageResultDto> StartOrSendAsync(string recipientUserId, string body, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("You must be signed in to send a message.");
        if (userId == recipientUserId)
            throw new InvalidOperationException("You can't message yourself.");
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Message can't be empty.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var (userAId, userBId) = OrderPair(userId, recipientUserId);
        var now = DateTime.UtcNow;

        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.UserAId == userAId && c.UserBId == userBId, ct);

        if (conversation is null)
        {
            var friends = await friendService.AreUsersFriendsAsync(userId, recipientUserId, ct);
            conversation = new Conversation
            {
                UserAId = userAId,
                UserBId = userBId,
                RequestedByUserId = userId,
                Status = friends ? ConversationStatus.Active : ConversationStatus.PendingApproval,
                CreatedAt = now,
                LastMessageAt = now
            };
            db.Conversations.Add(conversation);
        }
        else if (conversation.Status == ConversationStatus.PendingApproval)
        {
            throw new InvalidOperationException("A message request is already pending.");
        }
        else if (conversation.Status == ConversationStatus.Denied)
        {
            var cooldownExpired = conversation.RespondedAt is null || conversation.RespondedAt.Value.AddDays(CooldownDays) <= now;
            if (!cooldownExpired)
                throw new InvalidOperationException("This user previously denied your message request.");

            var friends = await friendService.AreUsersFriendsAsync(userId, recipientUserId, ct);
            conversation.Status = friends ? ConversationStatus.Active : ConversationStatus.PendingApproval;
            conversation.RequestedByUserId = userId;
            conversation.CreatedAt = now;
            conversation.RespondedAt = null;
            conversation.LastMessageAt = now;
        }
        else
        {
            conversation.LastMessageAt = now;
        }

        var trimmedBody = body.Trim();
        var message = new Message { Conversation = conversation, SenderUserId = userId, Body = trimmedBody, SentAt = now };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        if (conversation.Status == ConversationStatus.PendingApproval)
        {
            await notificationService.CreateAsync(recipientUserId, NotificationType.MessageRequestReceived,
                "New message request", "A DropShot user wants to send you a message.",
                $"/messages/{conversation.ConversationId}", conversation.ConversationId, ct: ct);
            await hubContext.Clients.User(recipientUserId)
                .SendAsync("ConversationRequestReceived", conversation.ConversationId, ct);
        }
        else
        {
            await notificationService.CreateAsync(recipientUserId, NotificationType.MessageReceived,
                "New message", Preview(trimmedBody), $"/messages/{conversation.ConversationId}",
                conversation.ConversationId, ct: ct);
            // IsMine is meaningless in a broadcast payload (it's caller-relative) —
            // clients compare SenderUserId to their own current user id instead.
            var dto = new MessageDto(message.MessageId, conversation.ConversationId, userId, trimmedBody, now, false);
            await hubContext.Clients.Group($"conversation-{conversation.ConversationId}")
                .SendAsync("ReceiveMessage", conversation.ConversationId, dto, ct);
            await hubContext.Clients.User(recipientUserId)
                .SendAsync("ReceiveMessage", conversation.ConversationId, dto, ct);
        }

        return new SendMessageResultDto(conversation.ConversationId, conversation.Status);
    }

    public async Task SendMessageAsync(int conversationId, string body, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(body)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId && (c.UserAId == userId || c.UserBId == userId), ct);
        if (conversation is null || conversation.Status != ConversationStatus.Active) return;

        var now = DateTime.UtcNow;
        var trimmedBody = body.Trim();
        var message = new Message { ConversationId = conversationId, SenderUserId = userId, Body = trimmedBody, SentAt = now };
        db.Messages.Add(message);
        conversation.LastMessageAt = now;
        await db.SaveChangesAsync(ct);

        var recipientId = conversation.UserAId == userId ? conversation.UserBId : conversation.UserAId;
        await notificationService.CreateAsync(recipientId, NotificationType.MessageReceived,
            "New message", Preview(trimmedBody), $"/messages/{conversationId}", conversationId, ct: ct);

        var dto = new MessageDto(message.MessageId, conversationId, userId, trimmedBody, now, false);
        await hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveMessage", conversationId, dto, ct);
        await hubContext.Clients.User(recipientId).SendAsync("ReceiveMessage", conversationId, dto, ct);
    }

    public async Task ApproveRequestAsync(int conversationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId && (c.UserAId == userId || c.UserBId == userId)
            && c.Status == ConversationStatus.PendingApproval && c.RequestedByUserId != userId, ct);
        if (conversation is null) return;

        conversation.Status = ConversationStatus.Active;
        conversation.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await notificationService.CreateAsync(conversation.RequestedByUserId, NotificationType.MessageRequestApproved,
            "Message request approved", null, $"/messages/{conversationId}", conversationId, ct: ct);
        await hubContext.Clients.User(conversation.RequestedByUserId)
            .SendAsync("ConversationRequestResolved", conversationId, true, ct);
    }

    public async Task DenyRequestAsync(int conversationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId && (c.UserAId == userId || c.UserBId == userId)
            && c.Status == ConversationStatus.PendingApproval && c.RequestedByUserId != userId, ct);
        if (conversation is null) return;

        conversation.Status = ConversationStatus.Denied;
        conversation.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await notificationService.CreateAsync(conversation.RequestedByUserId, NotificationType.MessageRequestDenied,
            "Message request denied", null, null, conversationId, ct: ct);
        await hubContext.Clients.User(conversation.RequestedByUserId)
            .SendAsync("ConversationRequestResolved", conversationId, false, ct);
    }

    public async Task MarkConversationReadAsync(int conversationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var isParticipant = await db.Conversations.AnyAsync(c =>
            c.ConversationId == conversationId && (c.UserAId == userId || c.UserBId == userId), ct);
        if (!isParticipant) return;

        var unread = await db.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderUserId != userId && m.ReadAt == null)
            .ToListAsync(ct);
        if (unread.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var m in unread) m.ReadAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnreadMessageCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return 0;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await (from m in db.Messages
                       join c in db.Conversations on m.ConversationId equals c.ConversationId
                       where m.SenderUserId != userId && m.ReadAt == null
                             && c.Status == ConversationStatus.Active
                             && (c.UserAId == userId || c.UserBId == userId)
                       select m.MessageId).CountAsync(ct);
    }
}
