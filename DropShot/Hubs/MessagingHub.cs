using DropShot.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Hubs;

/// <summary>
/// Push channel for direct messages and the Notification Centre. Carries
/// private content, unlike <c>ChatHub</c>/<c>QrAuthHub</c> — requires auth
/// on both the web (cookie) and MAUI (JWT bearer) hosts, and pushes are
/// scoped to the intended recipient via <see cref="Services.AppUserIdProvider"/>
/// (Clients.User) rather than an open group/broadcast. "Identity.Application"
/// is IdentityConstants.ApplicationScheme's value — hardcoded because
/// attribute arguments must be compile-time constants and that field is
/// static readonly, not const.
/// </summary>
[Authorize(AuthenticationSchemes = "Identity.Application,Bearer")]
public class MessagingHub(IDbContextFactory<MyDbContext> dbFactory) : Hub
{
    public async Task JoinConversation(int conversationId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var isParticipant = await db.Conversations.AnyAsync(c =>
            c.ConversationId == conversationId && (c.UserAId == userId || c.UserBId == userId));
        if (!isParticipant) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
    }

    public Task LeaveConversation(int conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
}
