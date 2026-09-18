namespace Novolis.Maui.WebView;

/// <summary>Content-Security-Policy helpers for HTML shown in a MAUI WebView.</summary>
public static class HtmlContentSecurityPolicy
{
    /// <summary>Restrictive policy for locally rendered Markdown (no script, data images only).</summary>
    public const string RestrictiveMarkdown =
        "default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-src 'none'; object-src 'none'";

    /// <summary>Inserts a CSP meta tag into an HTML document's <c>head</c>.</summary>
    /// <param name="html">Complete HTML document.</param>
    /// <param name="policy">CSP policy string.</param>
    /// <returns>HTML with the CSP meta inserted after <c>&lt;head&gt;</c>.</returns>
    public static string Apply(string html, string policy = RestrictiveMarkdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);

        var head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        if (head < 0)
            throw new InvalidDataException("HTML did not contain a head element.");

        var meta = $"<meta http-equiv=\"Content-Security-Policy\" content=\"{policy}\">";
        return html.Insert(head + "<head>".Length, meta);
    }
}
