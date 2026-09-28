using Markdig;

namespace DropShot.UI.Services;

/// <summary>
/// Renders the competition Description field (and any future Markdown user
/// content) into safe HTML for display via Blazor <c>MarkupString</c>.
///
/// HTML embedded in the input is disabled so that user-supplied content can't
/// inject <c>&lt;script&gt;</c> tags or arbitrary markup. Soft line breaks
/// render as <c>&lt;br&gt;</c> so plain newlines work without forcing two
/// trailing spaces per line.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseSoftlineBreakAsHardlineBreak()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseTaskLists()
        .Build();

    public static string ToHtml(string? markdown) =>
        string.IsNullOrWhiteSpace(markdown) ? "" : Markdown.ToHtml(markdown, _pipeline);
}
