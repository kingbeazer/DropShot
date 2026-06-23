using DropShot.Data;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Runs the fixture reminder sweep every hour: finds fixtures whose reminder
/// send-time has passed and emails the configured recipients. Errors are logged
/// but never crash the loop.
/// </summary>
public class FixtureReminderHostedService(
    IServiceProvider services,
    ILogger<FixtureReminderHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MyDbContext>>();
                var email = scope.ServiceProvider.GetRequiredService<AdminEmailService>();

                await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
                var result = await FixtureReminderService.RunSweepAsync(
                    db, DateTime.UtcNow, email, stoppingToken);

                if (result.RemindersSent > 0)
                {
                    logger.LogInformation(
                        "Fixture reminder sweep: {Count} reminder(s) sent.",
                        result.RemindersSent);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fixture reminder sweep failed; will retry next interval.");
            }

            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
