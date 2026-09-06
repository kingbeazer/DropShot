using DropShot.Data;
using DropShot.Shared;

namespace DropShot.Models;

/// <summary>
/// A 1:1 message thread between two <see cref="ApplicationUser"/> accounts.
/// UserAId/UserBId are stored as the ordinal-smaller/larger of the pair so a
/// unique index enforces at most one conversation per pair; RequestedByUserId
/// records who actually initiated it since A/B order is not chronological.
/// </summary>
public class Conversation
{
    public int ConversationId { get; set; }

    public string UserAId { get; set; } = "";
    public ApplicationUser UserA { get; set; } = null!;
    public string UserBId { get; set; } = "";
    public ApplicationUser UserB { get; set; } = null!;

    public string RequestedByUserId { get; set; } = "";
    public ApplicationUser RequestedByUser { get; set; } = null!;

    public ConversationStatus Status { get; set; } = ConversationStatus.PendingApproval;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    // Denormalized for cheap "list my conversations ordered by activity"
    // without a correlated subquery against Messages on every render.
    public DateTime? LastMessageAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
