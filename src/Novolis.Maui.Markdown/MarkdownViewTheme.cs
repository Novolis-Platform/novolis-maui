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
