using MudBlazor;

namespace DropShot.UI.Services;

public sealed class SiteAlertService : ISiteAlertService, IDisposable
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorLifetime = TimeSpan.FromSeconds(8);

    private readonly Lock _gate = new();
    private readonly List<SiteAlertEntry> _entries = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _timers = new();

    public event Action? OnChange;

    public SiteAlertEntry? Current
    {
        get { lock (_gate) return _entries.Count > 0 ? _entries[^1] : null; }
    }

    public IReadOnlyList<SiteAlertEntry> Entries
    {
        get { lock (_gate) return _entries.ToArray(); }
    }

    public void Add(
        string message,
        Severity severity = Severity.Normal,
        TimeSpan? autoDismissAfter = null,
        string? actionLabel = null,
        Func<Task>? actionCallback = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var lifetime = autoDismissAfter ?? (severity == Severity.Error ? ErrorLifetime : DefaultLifetime);
        var entry = new SiteAlertEntry(Guid.NewGuid(), message, severity, lifetime, actionLabel, actionCallback);

        CancellationToken token;
        lock (_gate)
        {
            _entries.Add(entry);
            var cts = new CancellationTokenSource();
            _timers[entry.Id] = cts;
            token = cts.Token;
        }

        OnChange?.Invoke();

        if (lifetime > TimeSpan.Zero)
        {
            _ = AutoDismissAsync(entry.Id, lifetime, token);
        }
    }

    public void Dismiss(Guid id)
    {
        lock (_gate)
        {
            var idx = _entries.FindIndex(e => e.Id == id);
            if (idx < 0) return;
            _entries.RemoveAt(idx);
            if (_timers.Remove(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
        OnChange?.Invoke();
    }

    public void DismissCurrent()
    {
        Guid? id;
        lock (_gate) id = _entries.Count > 0 ? _entries[^1].Id : null;
        if (id is { } value) Dismiss(value);
    }

    private async Task AutoDismissAsync(Guid id, TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            if (token.IsCancellationRequested) return;
            Dismiss(id);
        }
        catch (TaskCanceledException) { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var cts in _timers.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _timers.Clear();
            _entries.Clear();
        }
    }
}
