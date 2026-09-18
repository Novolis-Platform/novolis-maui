using MauiWebView = Microsoft.Maui.Controls.WebView;

namespace Novolis.Maui.WebView;

/// <summary>
/// MAUI WebView that shows local HTML only: CSP is the caller's responsibility,
/// in-view navigation is cancelled, and http(s)/mailto URIs are raised for the host to open.
/// </summary>
public sealed class SecureHtmlWebView : ContentView
{
    /// <summary>Bindable HTML document shown in the WebView.</summary>
    public static readonly BindableProperty HtmlProperty = BindableProperty.Create(
        nameof(Html),
        typeof(string),
        typeof(SecureHtmlWebView),
        default(string),
        propertyChanged: OnHtmlChanged);

    private readonly MauiWebView _viewer = new();

    /// <summary>Creates a locked-down HTML WebView.</summary>
    public SecureHtmlWebView()
    {
        Content = _viewer;
        _viewer.Navigating += OnNavigating;
    }

    /// <summary>Raised when the user activates an http(s) or mailto URI that must open outside the WebView.</summary>
    public event EventHandler<Uri>? ExternalNavigationRequested;

    /// <summary>Complete HTML document assigned to <see cref="HtmlWebViewSource"/>.</summary>
    public string? Html
    {
        get => (string?)GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    /// <summary>Inner MAUI WebView (for automation ids and host chrome).</summary>
    public MauiWebView InnerView => _viewer;

    private static void OnHtmlChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SecureHtmlWebView view)
            return;

        var html = newValue as string;
        view._viewer.Source = string.IsNullOrWhiteSpace(html)
            ? null
            : new HtmlWebViewSource { Html = html };
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs args)
    {
        var decision = WebViewNavigationPolicy.Decide(args.Url);
        if (decision is WebViewNavigationDecision.Allow)
            return;

        args.Cancel = true;
        if (decision is not WebViewNavigationDecision.OpenExternally)
            return;

        if (!Uri.TryCreate(args.Url, UriKind.Absolute, out var uri))
            return;

        var handler = ExternalNavigationRequested;
        if (handler is not null)
        {
            handler.Invoke(this, uri);
            return;
        }

        await Launcher.Default.OpenAsync(uri).ConfigureAwait(false);
    }
}
