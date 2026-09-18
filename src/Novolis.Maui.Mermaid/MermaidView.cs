using Novolis.Markup.Mermaid;
using Novolis.Maui.WebView;

namespace Novolis.Maui.Mermaid;

/// <summary>
/// MAUI view that renders Mermaid diagram source (or an <see cref="IMermaidable"/> builder)
/// to SVG via <c>Novolis.Markup.Mermaid.Rendering</c> and displays it in a locked-down WebView.
/// </summary>
public sealed class MermaidView : ContentView
{
    /// <summary>Raw Mermaid source. Used when <see cref="Diagram"/> is null.</summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(MermaidView),
        default(string),
        propertyChanged: OnDiagramChanged);

    /// <summary>Optional fluent Mermaid builder. When set, takes precedence over <see cref="Source"/>.</summary>
    public static readonly BindableProperty DiagramProperty = BindableProperty.Create(
        nameof(Diagram),
        typeof(IMermaidable),
        typeof(MermaidView),
        default(IMermaidable),
        propertyChanged: OnDiagramChanged);

    /// <summary>Color theme for SVG render.</summary>
    public static readonly BindableProperty DiagramThemeProperty = BindableProperty.Create(
        nameof(DiagramTheme),
        typeof(MermaidViewTheme),
        typeof(MermaidView),
        MermaidViewTheme.StudioDark,
        propertyChanged: OnDiagramChanged);

    private readonly SecureHtmlWebView _web = new();

    /// <summary>Creates an empty Mermaid view.</summary>
    public MermaidView()
    {
        Content = _web;
        _web.InnerView.AutomationId = "MermaidViewer";
    }

    /// <summary>Gets or sets raw Mermaid source.</summary>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Gets or sets a fluent diagram builder.</summary>
    public IMermaidable? Diagram
    {
        get => (IMermaidable?)GetValue(DiagramProperty);
        set => SetValue(DiagramProperty, value);
    }

    /// <summary>Gets or sets the render theme.</summary>
    public MermaidViewTheme DiagramTheme
    {
        get => (MermaidViewTheme)GetValue(DiagramThemeProperty);
        set => SetValue(DiagramThemeProperty, value);
    }

    /// <summary>Forces a re-render from current <see cref="Diagram"/> / <see cref="Source"/>.</summary>
    public void Refresh()
    {
        var mermaid = Diagram?.GetMermaidString() ?? Source;
        _web.Html = string.IsNullOrWhiteSpace(mermaid)
            ? null
            : MermaidHtml.ToHtmlDocument(mermaid, DiagramTheme);
    }

    private static void OnDiagramChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MermaidView view)
            view.Refresh();
    }
}
