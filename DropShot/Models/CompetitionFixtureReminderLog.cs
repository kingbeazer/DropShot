namespace DropShot.Models;

/// <summary>
/// Records that a <see cref="CompetitionFixtureReminder"/> was sent for a
/// specific fixture. Used to deduplicate — the sweep skips any
/// (fixture, reminder) pair that already has a log row.
/// </summary>
public class CompetitionFixtureReminderLog
{
    public int CompetitionFixtureReminderLogId { get; set; }
    /// <summary>Null when the email was sent using the default template.</summary>
    public int? CompetitionFixtureReminderId { get; set; }
    public int CompetitionFixtureId { get; set; }
    public DateTime SentAt { get; set; }

    public CompetitionFixtureReminder? Reminder { get; set; }
    public CompetitionFixture Fixture { get; set; } = null!;
}
