namespace Novolis.Maui.WebView;

/// <summary>Decision for a WebView navigation attempt.</summary>
public enum WebViewNavigationDecision
{
    /// <summary>Allow the WebView to load the URL (blank / about:blank).</summary>
    Allow,

    /// <summary>Cancel the navigation and do nothing.</summary>
    Cancel,

    /// <summary>Cancel in-WebView navigation and open the URI in the system handler.</summary>
    OpenExternally,
}

/// <summary>Navigation policy for locked-down document WebViews.</summary>
public static class WebViewNavigationPolicy
{
    /// <summary>Classifies a navigating URL for a document viewer that never follows in-page links.</summary>
    public static WebViewNavigationDecision Decide(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return WebViewNavigationDecision.Allow;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return WebViewNavigationDecision.Cancel;

        return uri.Scheme is "http" or "https" or "mailto"
            ? WebViewNavigationDecision.OpenExternally
            : WebViewNavigationDecision.Cancel;
    }
}
