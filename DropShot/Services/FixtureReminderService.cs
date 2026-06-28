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
///
/// When a competition has no custom reminders configured the default template
/// defined in <see cref="FixtureReminderDefaults"/> is used automatically.
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
        // Load fixtures that are scheduled in the future or recently, not yet complete.
        var windowStart = utcNow.AddHours(-25);
        var fixtures = await db.CompetitionFixtures
            .AsNoTracking()
            .Include(f => f.Competition)
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

        // Load all configured reminders.
        var reminders = await db.CompetitionFixtureReminders
            .AsNoTracking()
            .ToListAsync(ct);

        if (reminders.Count == 0) return new SweepResult(0);

        // Load already-sent logs so we can skip duplicates.
        var fixtureIds = fixtures.Select(f => f.CompetitionFixtureId).ToHashSet();
        var sentLogs = await db.CompetitionFixtureReminderLogs
            .Where(l => fixtureIds.Contains(l.CompetitionFixtureId))
            .Select(l => new { l.CompetitionFixtureReminderId, l.CompetitionFixtureId })
            .ToListAsync(ct);
        var sentSet = sentLogs
            .Select(l => (ReminderId: l.CompetitionFixtureReminderId, l.CompetitionFixtureId))
            .ToHashSet();

        int sent = 0;

        // ── Custom reminders ─────────────────────────────────────────────────
        foreach (var reminder in reminders)
        {
            var competitionFixtures = fixtures
                .Where(f => f.CompetitionId == reminder.CompetitionId)
                .ToList();

            foreach (var fixture in competitionFixtures)
            {
                if (fixture.ScheduledAt is null) continue;
                var sendAt = fixture.ScheduledAt.Value.AddHours(-reminder.HoursBefore);

                if (sendAt > utcNow) continue;
                if (utcNow > fixture.ScheduledAt.Value.AddHours(1)) continue;

                var key = ((int?)reminder.CompetitionFixtureReminderId, fixture.CompetitionFixtureId);
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
