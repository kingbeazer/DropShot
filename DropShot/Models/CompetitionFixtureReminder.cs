namespace DropShot.Models;

/// <summary>
/// A scheduled email reminder for a competition's fixtures. Multiple reminders
/// can be configured per competition (e.g. 48 h before and 24 h before). The
/// sweep fires hourly and sends each reminder once per fixture, logging delivery
/// in <see cref="CompetitionFixtureReminderLog"/>.
/// </summary>
public class CompetitionFixtureReminder
{
    public int CompetitionFixtureReminderId { get; set; }
    public int CompetitionId { get; set; }

    /// <summary>How many hours before the fixture's <c>ScheduledAt</c> to send this reminder.</summary>
    public int HoursBefore { get; set; }

    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    public Competition Competition { get; set; } = null!;
    public ICollection<CompetitionFixtureReminderLog> Logs { get; set; } = [];
}
