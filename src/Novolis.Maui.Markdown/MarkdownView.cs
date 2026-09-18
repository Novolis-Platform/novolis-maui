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

    private readonly SecureHtmlWebView _web = new();

    /// <summary>Creates a Markdown viewer.</summary>
    public MarkdownView()
    {
        Content = _web;
        _web.InnerView.AutomationId = "DocumentViewer";
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

    /// <summary>Inner locked-down WebView.</summary>
    public SecureHtmlWebView WebView => _web;

    /// <summary>Refreshes the preview from the current content.</summary>
    public void Refresh()
    {
        var explicitHtml = Html;
        _web.Html = explicitHtml is not null
            ? explicitHtml
            : MarkdownHtml.FromMarkdown(Markdown ?? string.Empty, Theme, Title);
    }

    private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MarkdownView view)
            view.Refresh();
    }
}
