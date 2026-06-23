using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Sweeps for fixture reminder emails that are due to be sent. Called hourly
/// by <see cref="FixtureReminderHostedService"/>. Finds every (fixture, reminder)
/// pair where <c>ScheduledAt - HoursBefore hours</c> has passed and no log entry
/// exists yet, then sends the email and writes a log row.
/// </summary>
public static class FixtureReminderService
{
    public record SweepResult(int RemindersSent);

    public static async Task<SweepResult> RunSweepAsync(
        MyDbContext db,
        DateTime utcNow,
        AdminEmailService emailService,
        CancellationToken ct = default)
    {
        // Load all active reminders alongside their competition.
        var reminders = await db.CompetitionFixtureReminders
            .AsNoTracking()
            .Include(r => r.Competition)
            .ToListAsync(ct);

        if (reminders.Count == 0) return new SweepResult(0);

        // Load fixtures that are scheduled in the future or recently, not yet complete.
        // We fetch a window of the past 25 hours (max HoursBefore we'd care about)
        // plus any upcoming to avoid missing reminders near the send time.
        var windowStart = utcNow.AddHours(-25);
        var fixtures = await db.CompetitionFixtures
            .AsNoTracking()
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .Include(f => f.Player3)
            .Include(f => f.Player4)
            .Include(f => f.HomeTeam).ThenInclude(t => t!.Participants).ThenInclude(p => p.Player)
            .Include(f => f.AwayTeam).ThenInclude(t => t!.Participants).ThenInclude(p => p.Player)
            .Where(f => f.ScheduledAt != null
                     && f.ScheduledAt >= windowStart
                     && f.Status == FixtureStatus.Scheduled)
            .ToListAsync(ct);

        if (fixtures.Count == 0) return new SweepResult(0);

        // Load already-sent logs so we can skip duplicates.
        var fixtureIds = fixtures.Select(f => f.CompetitionFixtureId).ToHashSet();
        var sentLogs = await db.CompetitionFixtureReminderLogs
            .Where(l => fixtureIds.Contains(l.CompetitionFixtureId))
            .Select(l => new { l.CompetitionFixtureReminderId, l.CompetitionFixtureId })
            .ToListAsync(ct);
        var sentSet = sentLogs
            .Select(l => (l.CompetitionFixtureReminderId, l.CompetitionFixtureId))
            .ToHashSet();

        int sent = 0;
        foreach (var reminder in reminders)
        {
            var competitionFixtures = fixtures
                .Where(f => f.CompetitionId == reminder.CompetitionId)
                .ToList();

            foreach (var fixture in competitionFixtures)
            {
                if (fixture.ScheduledAt is null) continue;
                var sendAt = fixture.ScheduledAt.Value.AddHours(-reminder.HoursBefore);

                // Only send if the send-time has passed in this sweep window.
                if (sendAt > utcNow) continue;
                // Don't send if the match has already started (more than 1 hour past ScheduledAt).
                if (utcNow > fixture.ScheduledAt.Value.AddHours(1)) continue;

                var key = (reminder.CompetitionFixtureReminderId, fixture.CompetitionFixtureId);
                if (sentSet.Contains(key)) continue;

                await emailService.SendFixtureReminderAsync(fixture, reminder, ct);

                var log = new CompetitionFixtureReminderLog
                {
                    CompetitionFixtureReminderId = reminder.CompetitionFixtureReminderId,
                    CompetitionFixtureId = fixture.CompetitionFixtureId,
                    SentAt = utcNow,
                };
                db.CompetitionFixtureReminderLogs.Add(log);
                await db.SaveChangesAsync(ct);
                sentSet.Add(key);
                sent++;
            }
        }

        return new SweepResult(sent);
    }
}
