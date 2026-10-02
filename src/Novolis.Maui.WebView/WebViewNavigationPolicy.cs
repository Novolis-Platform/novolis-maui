namespace Novolis.Maui.WebView;

/// <summary>Navigation policy for locked-down document WebViews.</summary>
public static class WebViewNavigationPolicy
{
    /// <summary>Classifies a navigating URL for a document viewer that never follows in-page links.</summary>
    public static WebViewNavigationDecision Decide(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return WebViewNavigationDecision.Allow;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return WebViewNavigationDecision.Cancel;

        // NavigateToString / HtmlWebViewSource uses about:blank, about:srcdoc, or a data: document.
        if (uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return WebViewNavigationDecision.Allow;
        }

        if (uri.Scheme is "http" or "https" or "mailto")
            return WebViewNavigationDecision.OpenExternally;
        if (string.Equals(uri.Scheme, "novolis-md", StringComparison.OrdinalIgnoreCase))
            return WebViewNavigationDecision.HandleInternally;
        return WebViewNavigationDecision.Cancel;
    }
}
