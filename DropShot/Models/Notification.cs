using DropShot.Data;
using DropShot.Shared;

namespace DropShot.Models;

/// <summary>
/// A generic, persisted notification for the Notification Centre. Not
/// messaging-specific — <see cref="Type"/> plus the optional ReferenceId/
/// PayloadJson let future features (fixture reminders, club admin requests,
/// etc.) post into the same system without a schema change.
/// </summary>
public class Notification
{
    public int NotificationId { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public NotificationType Type { get; set; }

    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }

    // No FK constraint — the target table depends on Type.
    public int? ReferenceId { get; set; }
    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
