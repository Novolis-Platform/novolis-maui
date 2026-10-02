using Novolis.Maui.WebView;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Markdown;

/// <summary>Tiny CSP HTML document that can paint mermaid SVG and raster images.</summary>
public static class MediaPreviewHtml
{
    /// <summary>Wraps a <c>data:</c> image URI in a themed, script-free page.</summary>
    public static string Wrap(string dataUri, string? caption = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUri);
        var src = System.Net.WebUtility.HtmlEncode(dataUri);
        var alt = System.Net.WebUtility.HtmlEncode(caption ?? "Preview");
        var background = ToCss(Profile.Background);
        return HtmlContentSecurityPolicy.Apply(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>" + alt + "</title><style>"
            + "html,body{margin:0;height:100%;background:" + background + ";"
            + "display:flex;align-items:center;justify-content:center;overflow:hidden}"
            + "img{max-width:100%;max-height:100%;object-fit:contain}"
            + "</style></head><body><img src=\"" + src + "\" alt=\"" + alt + "\"></body></html>");
    }

    private static string ToCss(Color color) =>
        $"#{(int)(color.Red * 255):X2}{(int)(color.Green * 255):X2}{(int)(color.Blue * 255):X2}";
}
