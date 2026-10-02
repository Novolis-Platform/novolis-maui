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
    private string? _documentFileUri;
    private string? _appliedHtml;

    /// <summary>Creates a locked-down HTML WebView.</summary>
    public SecureHtmlWebView()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        _viewer.HorizontalOptions = LayoutOptions.Fill;
        _viewer.VerticalOptions = LayoutOptions.Fill;
        Content = _viewer;
        _viewer.Navigating += OnNavigating;
        _viewer.HandlerChanged += (_, _) => ApplyHtml();
        _viewer.Loaded += (_, _) => ApplyHtml();
    }

    /// <summary>Raised when the user activates an http(s) or mailto URI that must open outside the WebView.</summary>
    public event EventHandler<Uri>? ExternalNavigationRequested;

    /// <summary>Raised when the user activates a host-handled custom scheme such as <c>novolis-md</c>.</summary>
    public event EventHandler<Uri>? HostNavigationRequested;

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
        if (bindable is SecureHtmlWebView view)
            view.ApplyHtml();
    }

    private void ApplyHtml()
    {
        var html = Html;
        if (string.Equals(html, _appliedHtml, StringComparison.Ordinal) && _viewer.Source is not null)
            return;

        HtmlTempDocument.TryDelete(_documentFileUri);
        _documentFileUri = null;
        _appliedHtml = html;
        if (string.IsNullOrWhiteSpace(html))
        {
            _viewer.Source = null;
            return;
        }

        // WinUI NavigateToString stays blank or refuses large documents. Load from a temp file instead.
        if (OperatingSystem.IsWindows())
        {
            _documentFileUri = HtmlTempDocument.Write(html);
            _viewer.Source = new UrlWebViewSource { Url = _documentFileUri };
            return;
        }

        _viewer.Source = new HtmlWebViewSource { Html = html };
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs args)
    {
        if (HtmlTempDocument.IsSameDocument(args.Url, _documentFileUri))
            return;

        var decision = WebViewNavigationPolicy.Decide(args.Url);
        if (decision is WebViewNavigationDecision.Allow)
            return;

        args.Cancel = true;
        if (decision is WebViewNavigationDecision.HandleInternally)
        {
            if (Uri.TryCreate(args.Url, UriKind.Absolute, out var hostUri))
                HostNavigationRequested?.Invoke(this, hostUri);
            return;
        }

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
