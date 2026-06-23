namespace DropShot.Models;

/// <summary>
/// Records that a <see cref="CompetitionFixtureReminder"/> was sent for a
/// specific fixture. Used to deduplicate — the sweep skips any
/// (fixture, reminder) pair that already has a log row.
/// </summary>
public class CompetitionFixtureReminderLog
{
    public int CompetitionFixtureReminderLogId { get; set; }
    public int CompetitionFixtureReminderId { get; set; }
    public int CompetitionFixtureId { get; set; }
    public DateTime SentAt { get; set; }

    public CompetitionFixtureReminder Reminder { get; set; } = null!;
    public CompetitionFixture Fixture { get; set; } = null!;
}
