using Microsoft.Extensions.DependencyInjection;

namespace DropShot.Services;

/// <summary>
/// Fire-and-forget runner for work that shouldn't block the caller's response —
/// primarily outbound email. Each task runs inside a fresh DI scope so it doesn't
/// depend on the caller's (possibly soon-disposed) scope, and exceptions are
/// logged rather than surfaced as unobserved-task-exception crashes.
///
/// Intended for "best effort" side-effects. Don't use this for work the user is
/// about to observe (e.g. bracket progression) — those should stay awaited.
/// </summary>
public class BackgroundTaskQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundTaskQueue> logger)
{
    public void Run(string description, Func<IServiceProvider, Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await work(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background task '{Description}' failed", description);
            }
        });
    }
}
