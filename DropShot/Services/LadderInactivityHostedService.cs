using DropShot.Data;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Runs the SinglesLadder inactivity sweep every 24 hours: decays idle
/// participants by <see cref="LadderInactivityService.DecayPointsPerWeek"/>
/// points per week past the grace window, and emails a warning to players
/// approaching it. Errors are logged but never crash the loop.
/// </summary>
public class LadderInactivityHostedService(
    IServiceProvider services,
    ILogger<LadderInactivityHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let app startup / DI / EF model warm up before the first sweep.
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
                var result = await LadderInactivityService.RunSweepAsync(
                    db, DateTime.UtcNow, email, stoppingToken);

                if (result.DecayEventsApplied > 0 || result.WarningsSent > 0)
                {
                    logger.LogInformation(
                        "Ladder inactivity sweep: {Decays} decays applied, {Warnings} warnings sent.",
                        result.DecayEventsApplied, result.WarningsSent);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ladder inactivity sweep failed; will retry next interval.");
            }

            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
