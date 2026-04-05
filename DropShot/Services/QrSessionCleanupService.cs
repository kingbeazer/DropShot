namespace DropShot.Services;

public class QrSessionCleanupService(QrLoginService qrLoginService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            qrLoginService.CleanupExpiredSessions();
        }
    }
}
