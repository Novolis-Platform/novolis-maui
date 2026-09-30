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
