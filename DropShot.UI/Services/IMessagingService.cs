using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

public interface IMessagingService
{
    Task<List<ConversationSummaryDto>> GetActiveConversationsAsync(CancellationToken ct = default);

    /// <summary>Pending conversations where the current user is the recipient, not the requester.</summary>
    Task<List<ConversationSummaryDto>> GetIncomingMessageRequestsAsync(CancellationToken ct = default);

    Task<ConversationDetailDto?> GetConversationAsync(int conversationId, CancellationToken ct = default);

    /// <summary>
    /// Returns empty when the conversation is still PendingApproval and the
    /// caller isn't the requester — message bodies stay hidden until Allow.
    /// </summary>
    Task<List<MessageDto>> GetMessagesAsync(int conversationId, int? beforeMessageId, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Creates the conversation (Active if friends, PendingApproval otherwise)
    /// plus the first message, or appends to an existing Active conversation.
    /// Throws if a request is already pending, or denied within the cooldown.
    /// </summary>
    Task<SendMessageResultDto> StartOrSendAsync(string recipientUserId, string body, CancellationToken ct = default);

    /// <summary>Sends into an already-Active conversation only; no-op otherwise.</summary>
    Task SendMessageAsync(int conversationId, string body, CancellationToken ct = default);

    Task ApproveRequestAsync(int conversationId, CancellationToken ct = default);
    Task DenyRequestAsync(int conversationId, CancellationToken ct = default);

    Task MarkConversationReadAsync(int conversationId, CancellationToken ct = default);
    Task<int> GetUnreadMessageCountAsync(CancellationToken ct = default);
}
