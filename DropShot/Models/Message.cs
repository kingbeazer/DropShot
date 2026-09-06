using DropShot.Data;

namespace DropShot.Models;

public class Message
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public string SenderUserId { get; set; } = "";
    public ApplicationUser Sender { get; set; } = null!;

    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // 1:1 conversation, so a single nullable timestamp is enough — there's
    // only ever one "other" reader, unlike a group chat.
    public DateTime? ReadAt { get; set; }
}
