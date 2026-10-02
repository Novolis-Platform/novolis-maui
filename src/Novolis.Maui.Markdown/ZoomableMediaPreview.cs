using Novolis.Maui.WebView;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Markdown;

/// <summary>Fullscreen mermaid or image preview with pinch, pan, and button zoom.</summary>
public sealed class ZoomableMediaPreview : Grid
{
    private const double MinimumZoom = 0.4;
    private const double MaximumZoom = 8;
    private readonly SecureHtmlWebView _surface = new()
    {
        AutomationId = "MediaPreviewImage",
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };
    private readonly Label _caption = new()
    {
        FontSize = 13,
        LineBreakMode = LineBreakMode.TailTruncation,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
    };
    private double _zoom = 1;
    private double _pinchStart = 1;
    private bool _panning;
    private double _panX;
    private double _panY;

    /// <summary>Creates a hidden preview overlay.</summary>
    public ZoomableMediaPreview()
    {
        IsVisible = false;
        AutomationId = "MediaPreview";
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        var close = ChromeButton("Close", "MediaPreviewClose", Hide);
        var zoomOut = ChromeButton("−", "MediaPreviewZoomOut", () => SetZoom(_zoom / 1.25));
        var zoomIn = ChromeButton("+", "MediaPreviewZoomIn", () => SetZoom(_zoom * 1.25));
        var reset = ChromeButton("Reset", "MediaPreviewReset", () => SetZoom(1));
        var bar = new Grid
        {
            Padding = new Thickness(16, 12),
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        bar.Add(_caption, 0, 0);
        bar.Add(zoomOut, 1, 0);
        bar.Add(zoomIn, 2, 0);
        bar.Add(reset, 3, 0);
        bar.Add(close, 4, 0);

        var stage = new Grid { IsClippedToBounds = true };
        stage.Add(_surface);
        var overlay = new BoxView
        {
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPan;
        overlay.GestureRecognizers.Add(pinch);
        overlay.GestureRecognizers.Add(pan);
        stage.Add(overlay);

        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        Add(bar);
        Grid.SetRow(bar, 0);
        Add(stage);
        Grid.SetRow(stage, 1);
        ApplyChrome();
    }

    /// <summary>Raised after <see cref="Hide"/> so a host can restore its document view.</summary>
    public event EventHandler? Closed;

    /// <summary>Shows a mermaid or image <c>data:</c> URI full screen.</summary>
    public void Show(string dataUri, string caption)
    {
        _surface.Html = MediaPreviewHtml.Wrap(dataUri, caption);
        _caption.Text = caption;
        SetZoom(1);
        IsVisible = true;
    }

    /// <summary>Hides the overlay.</summary>
    public void Hide()
    {
        IsVisible = false;
        _surface.Html = null;
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
        _surface.Scale = _zoom;
        if (System.Math.Abs(_zoom - 1) < 0.01)
        {
            _surface.TranslationX = 0;
            _surface.TranslationY = 0;
        }
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs args)
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

    private void OnPan(object? sender, PanUpdatedEventArgs args)
    {
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
