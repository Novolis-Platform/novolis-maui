using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Markdown;

/// <summary>Fullscreen mermaid/image chrome: pinch, pan, and button zoom over a host surface.</summary>
public sealed class ZoomableMediaPreview : Grid
{
    private const double MinimumZoom = 0.4;
    private const double MaximumZoom = 8;
    private readonly Label _caption = new()
    {
        FontSize = 13,
        LineBreakMode = LineBreakMode.TailTruncation,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
    };
    private View? _surface;
    private double _zoom = 1;
    private double _pinchStart = 1;
    private bool _panning;
    private double _panX;
    private double _panY;

    /// <summary>Creates hidden preview chrome.</summary>
    public ZoomableMediaPreview()
    {
        IsVisible = false;
        AutomationId = "MediaPreview";
        HorizontalOptions = LayoutOptions.Fill;
        var close = ChromeButton("Close", "MediaPreviewClose", Hide);
        var zoomOut = ChromeButton("−", "MediaPreviewZoomOut", () => SetZoom(_zoom / 1.25));
        var zoomIn = ChromeButton("+", "MediaPreviewZoomIn", () => SetZoom(_zoom * 1.25));
        var reset = ChromeButton("Reset", "MediaPreviewReset", () => SetZoom(1));
        Padding = new Thickness(16, 12);
        ColumnSpacing = 8;
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Add(_caption);
        Grid.SetColumn(_caption, 0);
        Add(zoomOut);
        Grid.SetColumn(zoomOut, 1);
        Add(zoomIn);
        Grid.SetColumn(zoomIn, 2);
        Add(reset);
        Grid.SetColumn(reset, 3);
        Add(close);
        Grid.SetColumn(close, 4);
        ApplyChrome();
    }

    /// <summary>Raised after <see cref="Hide"/> so a host can restore the document.</summary>
    public event EventHandler? Closed;

    /// <summary>Shows chrome and zooms <paramref name="surface"/>.</summary>
    public void Show(View surface, string caption)
    {
        _surface = surface;
        _caption.Text = caption;
        SetZoom(1);
        IsVisible = true;
    }

    /// <summary>Hides chrome and resets zoom on the attached surface.</summary>
    public void Hide()
    {
        IsVisible = false;
        SetZoom(1);
        _surface = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyChrome()
    {
        _caption.TextColor = Profile.Text;
        BackgroundColor = Profile.Background;
    }

    private void SetZoom(double zoom)
    {
        _zoom = System.Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (_surface is null)
            return;
        _surface.Scale = _zoom;
        if (System.Math.Abs(_zoom - 1) < 0.01)
        {
            _surface.TranslationX = 0;
            _surface.TranslationY = 0;
        }
    }

    internal void Pinch(PinchGestureUpdatedEventArgs args)
    {
        switch (args.Status)
        {
            case GestureStatus.Started:
                _pinchStart = _zoom;
                break;
            case GestureStatus.Running:
                SetZoom(_pinchStart * args.Scale);
                break;
        }
    }

    internal void Pan(PanUpdatedEventArgs args)
    {
        if (_surface is null)
            return;
        switch (args.StatusType)
        {
            case GestureStatus.Started:
                _panning = true;
                _panX = args.TotalX;
                _panY = args.TotalY;
                break;
            case GestureStatus.Running:
                if (!_panning)
                {
                    _panning = true;
                    _panX = args.TotalX;
                    _panY = args.TotalY;
                    break;
                }

                _surface.TranslationX += args.TotalX - _panX;
                _surface.TranslationY += args.TotalY - _panY;
                _panX = args.TotalX;
                _panY = args.TotalY;
                break;
            default:
                _panning = false;
                break;
        }
    }

    private static Button ChromeButton(string text, string automationId, Action click)
    {
        var button = new Button
        {
            AutomationId = automationId,
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            CornerRadius = 16,
            Padding = new Thickness(14, 8),
            BackgroundColor = Profile.AccentFill,
            TextColor = Profile.OnAccentFill,
        };
        button.Clicked += (_, _) => click();
        return button;
    }
}
