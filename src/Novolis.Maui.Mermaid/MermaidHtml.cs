using System.Net;
using System.Text;
using Novolis.Markup.Mermaid.Rendering;
using Novolis.Maui.WebView;

namespace Novolis.Maui.Mermaid;

/// <summary>Renders Mermaid source to SVG / HTML fragments for MAUI hosts.</summary>
public static class MermaidHtml
{
    /// <summary>Renders Mermaid source to an SVG document string, or <c>null</c> on failure.</summary>
    public static string? TryRenderSvg(string? mermaid, MermaidViewTheme theme = MermaidViewTheme.StudioDark) =>
        MermaidSvgRenderer.TryRenderSvg(mermaid, Map(theme));

    /// <summary>Wraps a rendered SVG as a base64 <c>&lt;img&gt;</c> HTML fragment.</summary>
    public static string? TryRenderHtmlImage(string? mermaid, MermaidViewTheme theme = MermaidViewTheme.StudioDark)
    {
        var svg = TryRenderSvg(mermaid, theme);
        if (svg is null)
            return null;

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"""
               <div class="mermaid-diagram">
               <img alt="Mermaid diagram" src="data:image/svg+xml;base64,{base64}" />
               </div>
               """;
    }

    /// <summary>Fallback HTML when rendering fails.</summary>
    public static string RenderFallbackPre(string mermaid) =>
        $"""
         <pre class="mermaid-source"><code>{WebUtility.HtmlEncode(mermaid)}</code></pre>
         """;

    /// <summary>Complete HTML document for a single diagram, with CSP applied.</summary>
    public static string ToHtmlDocument(string? mermaid, MermaidViewTheme theme = MermaidViewTheme.StudioDark)
    {
        var body = TryRenderHtmlImage(mermaid, theme) ?? RenderFallbackPre(mermaid ?? string.Empty);
        var bg = theme is MermaidViewTheme.GitHubLight ? "#ffffff" : "#1e1e1e";
        var fg = theme is MermaidViewTheme.GitHubLight ? "#24292f" : "#e8e8e8";
        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
            html, body { margin: 0; padding: 12px; background: {{bg}}; color: {{fg}}; font-family: system-ui, sans-serif; }
            .mermaid-diagram { overflow-x: auto; }
            .mermaid-diagram img { max-width: 100%; height: auto; display: block; }
            pre.mermaid-source { white-space: pre-wrap; font-size: 12px; }
            </style>
            </head>
            <body>{{body}}</body>
            </html>
            """;
        return HtmlContentSecurityPolicy.Apply(html);
    }

    internal static MermaidRenderTheme Map(MermaidViewTheme theme) =>
        theme is MermaidViewTheme.GitHubLight
            ? MermaidRenderTheme.GitHubLight
            : MermaidRenderTheme.StudioDark;
}
