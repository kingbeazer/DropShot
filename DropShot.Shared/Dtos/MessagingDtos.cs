namespace DropShot.Shared.Dtos;

public record ConversationSummaryDto(
    int ConversationId,
    string OtherUserId,
    string OtherUserDisplayName,
    string? OtherUserProfileImagePath,
    ConversationStatus Status,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    bool IsRequester,
    int UnreadCount);

public record ConversationDetailDto(
    int ConversationId,
    string OtherUserId,
    string OtherUserDisplayName,
    string? OtherUserProfileImagePath,
    ConversationStatus Status,
    bool IsRequester);

public record MessageDto(
    int MessageId,
    int ConversationId,
    string SenderUserId,
    string Body,
    DateTime SentAt,
    bool IsMine);

public record SendMessageResultDto(int ConversationId, ConversationStatus Status);
