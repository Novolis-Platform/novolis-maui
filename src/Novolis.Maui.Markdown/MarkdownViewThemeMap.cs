using Novolis.Markup.Markdown.Rendering;
using Novolis.Markup.Mermaid.Rendering;

namespace Novolis.Maui.Markdown;

internal static class MarkdownViewThemeMap
{
    public static (MarkdownHtmlTheme Html, MermaidRenderTheme Mermaid) Map(MarkdownViewTheme theme) =>
        theme switch
        {
            MarkdownViewTheme.GitHubLight => (MarkdownHtmlTheme.GitHubLight, MermaidRenderTheme.GitHubLight),
            MarkdownViewTheme.GitHubDark => (MarkdownHtmlTheme.GitHubDark, MermaidRenderTheme.StudioDark),
            _ => (MarkdownHtmlTheme.StudioDark, MermaidRenderTheme.StudioDark),
        };
}
