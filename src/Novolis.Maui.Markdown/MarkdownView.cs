using Novolis.Markup.Markdown;
using Novolis.Maui.WebView;

namespace Novolis.Maui.Markdown;

/// <summary>Live HTML preview of Markdown source with themed Mermaid diagrams in a locked-down WebView.</summary>
public sealed class MarkdownView : ContentView
{
    /// <summary>Markdown source to render.</summary>
    public static readonly BindableProperty MarkdownProperty = BindableProperty.Create(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownView),
        default(string),
        propertyChanged: OnContentChanged);

    /// <summary>Optional pre-rendered HTML. When set, takes precedence over <see cref="Markdown"/>.</summary>
    public static readonly BindableProperty HtmlProperty = BindableProperty.Create(
        nameof(Html),
        typeof(string),
        typeof(MarkdownView),
        default(string),
        propertyChanged: OnContentChanged);

    /// <summary>Visual theme applied when rendering from <see cref="Markdown"/>.</summary>
    public static readonly BindableProperty ThemeProperty = BindableProperty.Create(
        nameof(Theme),
        typeof(MarkdownViewTheme),
        typeof(MarkdownView),
        MarkdownViewTheme.StudioDark,
        propertyChanged: OnContentChanged);

    /// <summary>Optional HTML document title.</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(MarkdownView),
        default(string),
        propertyChanged: OnContentChanged);

    /// <summary>Directory used to embed local Markdown images.</summary>
    public static readonly BindableProperty SourceDirectoryProperty = BindableProperty.Create(
        nameof(SourceDirectory),
        typeof(string),
        typeof(MarkdownView),
        default(string),
        propertyChanged: OnContentChanged);

    private readonly SecureHtmlWebView _web = new();
    private readonly ZoomableMediaPreview _preview = new();
    private MarkdownHtmlActionSink _actions = new();
    private bool _refreshQueued;
    private string? _renderedHtml;
    private int _refreshGeneration;

    /// <summary>Creates a Markdown viewer.</summary>
    public MarkdownView()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        _web.InnerView.AutomationId = "DocumentViewer";
        _web.HostNavigationRequested += OnHostNavigation;
        _preview.Closed += (_, _) => Content = _web;
        Content = _web;
    }

    /// <summary>Gets or sets Markdown source used when <see cref="Html"/> is not set.</summary>
    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Gets or sets pre-rendered HTML. When set, takes precedence over <see cref="Markdown"/>.</summary>
    public string? Html
    {
        get => (string?)GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    /// <summary>Gets or sets the HTML/Mermaid theme.</summary>
    public MarkdownViewTheme Theme
    {
        get => (MarkdownViewTheme)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Gets or sets the HTML document title when rendering from Markdown.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the document directory for local images.</summary>
    public string? SourceDirectory
    {
        get => (string?)GetValue(SourceDirectoryProperty);
        set => SetValue(SourceDirectoryProperty, value);
    }

    /// <summary>Inner locked-down WebView.</summary>
    public SecureHtmlWebView WebView => _web;

    /// <summary>Refreshes the preview from the current content.</summary>
    public void Refresh() => QueueRefresh();

    private void QueueRefresh()
    {
        if (_refreshQueued)
            return;
        _refreshQueued = true;
        Dispatcher.Dispatch(RefreshNow);
    }

    private void RefreshNow()
    {
        _refreshQueued = false;
        var explicitHtml = Html;
        if (explicitHtml is not null)
        {
            _actions = new MarkdownHtmlActionSink();
            AssignHtml(explicitHtml);
            return;
        }

        var markdown = Markdown ?? string.Empty;
        var theme = Theme;
        var title = Title;
        var directory = SourceDirectory;
        var generation = Interlocked.Increment(ref _refreshGeneration);
        AssignHtml(RenderingPlaceholder(title));
        _ = Task.Run(() =>
            {
                var text = MarkdownHtml.Build(markdown, theme, title, directory, renderMermaid: false);
                if (generation == _refreshGeneration)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (generation != _refreshGeneration)
                            return;
                        _actions = text.Actions;
                        AssignHtml(text.Document);
                    });
                }

                return MarkdownHtml.Build(markdown, theme, title, directory, renderMermaid: true);
            })
            .ContinueWith(
                task =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (generation != _refreshGeneration)
                            return;
                        if (task.IsFaulted)
                        {
                            var message = task.Exception?.GetBaseException().Message ?? "Unable to render Markdown.";
                            AssignHtml(
                                "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Markdown</title></head>"
                                + "<body><pre>" + System.Net.WebUtility.HtmlEncode(message) + "</pre></body></html>");
                            return;
                        }

                        _actions = task.Result.Actions;
                        AssignHtml(task.Result.Document);
                    });
                },
                TaskScheduler.Default);
    }

    private static string RenderingPlaceholder(string? title) =>
        HtmlContentSecurityPolicy.Apply(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>"
            + System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "Markdown" : title)
            + "</title></head><body style=\"margin:24px;font-family:Segoe UI,system-ui,sans-serif;"
            + "color:#e8e8e8;background:#0d1117\"><p>Rendering…</p></body></html>");

    private void AssignHtml(string html)
    {
        if (string.Equals(html, _renderedHtml, StringComparison.Ordinal))
            return;
        _renderedHtml = html;
        _web.Html = html;
    }

    private async void OnHostNavigation(object? sender, Uri uri)
    {
        if (!MarkdownHtmlActionUris.TryRead(uri, out var kind, out var index))
            return;

        if (kind is MarkdownHtmlActionKind.Copy)
        {
            if ((uint)index >= (uint)_actions.CodeBlocks.Count)
                return;
            await Clipboard.Default.SetTextAsync(_actions.CodeBlocks[index]).ConfigureAwait(true);
            return;
        }

        if ((uint)index >= (uint)_actions.Previews.Count)
            return;
        var asset = _actions.Previews[index];
        _preview.Show(asset.DataUri, asset.Caption);
        Content = _preview;
    }

    private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MarkdownView view)
            view.QueueRefresh();
    }
}
