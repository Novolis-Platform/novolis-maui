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
    private readonly Grid _root = new();
    private MarkdownHtmlActionSink _actions = new();
    private bool _refreshQueued;
    private string? _renderedHtml;

    /// <summary>Creates a Markdown viewer.</summary>
    public MarkdownView()
    {
        _web.InnerView.AutomationId = "DocumentViewer";
        _web.HostNavigationRequested += OnHostNavigation;
        _root.Add(_web);
        _root.Add(_preview);
        Content = _root;
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

        var built = MarkdownHtml.Build(Markdown ?? string.Empty, Theme, Title, SourceDirectory);
        _actions = built.Actions;
        AssignHtml(built.Document);
    }

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
        _preview.Show(MarkdownMediaSource.FromDataUri(asset.DataUri), asset.Caption);
    }

    private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MarkdownView view)
            view.QueueRefresh();
    }
}
