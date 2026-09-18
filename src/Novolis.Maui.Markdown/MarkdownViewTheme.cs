using Novolis.Markup.Markdown.Rendering;
using Novolis.Markup.Mermaid.Rendering;

namespace Novolis.Maui.Markdown;

/// <summary>HTML themes for the MAUI Markdown viewer (maps to Novolis.Markup HTML + Mermaid themes).</summary>
public enum MarkdownViewTheme
{
    /// <summary>Dark studio documentation look.</summary>
    StudioDark,

    /// <summary>GitHub light documentation look.</summary>
    GitHubLight,

    /// <summary>GitHub dark documentation look.</summary>
    GitHubDark,
}

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
