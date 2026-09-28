using MudBlazor;

namespace DropShot.UI.Services;

public sealed record SiteAlertEntry(
    Guid Id,
    string Message,
    Severity Severity,
    TimeSpan AutoDismissAfter,
    string? ActionLabel = null,
    Func<Task>? ActionCallback = null);

/// <summary>
/// Site-wide alert service. Replaces ad-hoc <c>ISnackbar.Add</c> calls so that
/// action / error notifications surface in an in-flow banner that pushes page
/// content down rather than floating over controls. Scoped per circuit; the
/// matching <c>SiteAlertHost</c> component subscribes to <see cref="OnChange"/>
/// to render and animate the active alert.
/// </summary>
public interface ISiteAlertService
{
    event Action? OnChange;
    SiteAlertEntry? Current { get; }
    IReadOnlyList<SiteAlertEntry> Entries { get; }

    void Add(
        string message,
        Severity severity = Severity.Normal,
        TimeSpan? autoDismissAfter = null,
        string? actionLabel = null,
        Func<Task>? actionCallback = null);

    void Dismiss(Guid id);
    void DismissCurrent();
}
