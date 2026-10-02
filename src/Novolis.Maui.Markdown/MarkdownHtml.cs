using Novolis.Markup.Markdown.Mermaid.Rendering;
using Novolis.Maui.WebView;

namespace Novolis.Maui.Markdown;

/// <summary>
/// Host-agnostic Markdown → HTML for MAUI WebViews.
/// Parse, theme CSS, and Mermaid SVG live in <c>Novolis.Markup.*</c>; this type only maps themes and applies CSP.
/// </summary>
public static class MarkdownHtml
{
    /// <summary>Converts Markdown source to a complete themed HTML document with CSP.</summary>
    public static string FromMarkdown(
        string markdown,
        MarkdownViewTheme theme = MarkdownViewTheme.StudioDark,
        string? title = null,
        string? sourceDirectory = null,
        bool renderMermaid = true) =>
        Build(markdown, theme, title, sourceDirectory, renderMermaid).Document;

    /// <summary>Converts Markdown to CSP HTML plus copy/preview payloads.</summary>
    public static MarkdownHtmlDocumentBuild Build(
        string markdown,
        MarkdownViewTheme theme = MarkdownViewTheme.StudioDark,
        string? title = null,
        string? sourceDirectory = null,
        bool renderMermaid = true)
    {
        var (htmlTheme, mermaidTheme) = MarkdownViewThemeMap.Map(theme);
        var built = MermaidMarkdownHtmlRenderer.Build(
            markdown ?? string.Empty,
            htmlTheme,
            mermaidTheme,
            title,
            sourceDirectory,
            renderMermaid);
        return built with { Document = HtmlContentSecurityPolicy.Apply(built.Document) };
    }
}
