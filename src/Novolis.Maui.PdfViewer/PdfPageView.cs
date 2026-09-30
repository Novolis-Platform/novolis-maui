using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
using Novolis.Pdf.Rendering;

namespace Novolis.Maui.PdfViewer;

/// <summary>Virtualized MAUI page cell that renders only while it is attached.</summary>
public sealed class PdfPageView : ContentView
{
    private readonly Image _image = new()
    {
        Aspect = Aspect.AspectFit,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };
    private readonly Label _placeholder = new()
    {
        Text = "Page",
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        Opacity = 0.55,
    };
    private readonly Border _paper;
    private CancellationTokenSource? _renderCancellation;
    private byte[]? _pngBytes;
    private int _pixelWidth;
    private int _pixelHeight;

    /// <summary>Identifies the page index rendered by this cell.</summary>
    public static readonly BindableProperty PageIndexProperty =
        BindableProperty.Create(
            nameof(PageIndex),
            typeof(int),
            typeof(PdfPageView),
            -1,
            propertyChanged: static (bindable, _, _) =>
                ((PdfPageView)bindable).QueueRender());

    /// <summary>Identifies the page display width.</summary>
    public static readonly BindableProperty PageWidthProperty =
        BindableProperty.Create(
            nameof(PageWidth),
            typeof(double),
            typeof(PdfPageView),
            320d,
            propertyChanged: static (bindable, oldValue, newValue) =>
            {
                var page = (PdfPageView)bindable;
                page.ApplySize();
                if (System.Math.Abs((double)oldValue - (double)newValue) >= 1)
                    page.QueueRender();
            });

    /// <summary>Identifies the page display height.</summary>
    public static readonly BindableProperty PageHeightProperty =
        BindableProperty.Create(
            nameof(PageHeight),
            typeof(double),
            typeof(PdfPageView),
            0d,
            propertyChanged: static (bindable, _, _) =>
                ((PdfPageView)bindable).ApplySize());

    /// <summary>Gets or sets the zero-based page index.</summary>
    public int PageIndex
    {
        get => (int)GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    /// <summary>Gets or sets the rendered page width.</summary>
    public double PageWidth
    {
        get => (double)GetValue(PageWidthProperty);
        set => SetValue(PageWidthProperty, value);
    }

    /// <summary>Gets or sets the rendered page height. When unset, height follows letter aspect.</summary>
    public double PageHeight
    {
        get => (double)GetValue(PageHeightProperty);
        set => SetValue(PageHeightProperty, value);
    }

    /// <summary>Provides the viewer's bounded page rendering callback.</summary>
    public Func<int, CancellationToken, Task<PdfRenderedPage>>? RenderPageAsync { get; set; }

    /// <summary>Receives a render failure for the current cell.</summary>
    public Action<Exception>? RenderFailed { get; set; }

    /// <summary>Last painted PNG for the MAUI UI agent.</summary>
    public byte[]? TryGetLastFramePng() => _pngBytes is { Length: > 0 } ? _pngBytes : null;

    /// <summary>Creates a virtualized page cell.</summary>
    public PdfPageView()
    {
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Start;
        Padding = 0;
        BackgroundColor = Colors.Transparent;
        _paper = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = new SolidColorBrush(Profile.Border),
            StrokeThickness = 1,
            Padding = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Content = new Grid
            {
                BackgroundColor = Colors.White,
                Children =
                {
                    _placeholder,
                    _image,
                },
            },
        };
        Content = _paper;
        ApplySize();
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        if (Handler is null)
            CancelRender();
        else
            QueueRender();
        base.OnHandlerChanged();
    }

    private void QueueRender()
    {
        if (Handler is null || PageIndex < 0 || RenderPageAsync is null)
            return;
        _ = RenderAsync(PageIndex);
    }

    private async Task RenderAsync(int pageIndex)
    {
        CancelRender();
        _renderCancellation = new CancellationTokenSource();
        try
        {
            var rendered = await RenderPageAsync!(pageIndex, _renderCancellation.Token)
                .ConfigureAwait(false);
            if (pageIndex != PageIndex || _renderCancellation.IsCancellationRequested)
                return;
            _pngBytes = rendered.PngBytes;
            _pixelWidth = rendered.PixelWidth;
            _pixelHeight = rendered.PixelHeight;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _placeholder.IsVisible = false;
                _image.Source = ImageSource.FromStream(
                    () => new MemoryStream(_pngBytes, writable: false));
                ApplySize();
                SemanticProperties.SetDescription(this, $"PDF page {pageIndex + 1}");
            });
        }
        catch (OperationCanceledException) when (_renderCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _placeholder.IsVisible = true;
                _placeholder.Text = "Page could not be rendered";
                _image.Source = null;
            });
            RenderFailed?.Invoke(exception);
        }
    }

    private void ApplySize()
    {
        if (PageWidth <= 0 || !double.IsFinite(PageWidth))
            return;
        var paperWidth = System.Math.Clamp(PageWidth, 80, 4096);
        var paperHeight = PageHeight > 0 && double.IsFinite(PageHeight)
            ? System.Math.Clamp(PageHeight, 80, 4096)
            : _pixelWidth > 0 && _pixelHeight > 0
                ? paperWidth * _pixelHeight / _pixelWidth
                : paperWidth * 1.5;
        paperHeight = System.Math.Clamp(paperHeight, 80, 4096);
        LockSize(this, paperWidth, paperHeight);
        LockSize(_paper, paperWidth, paperHeight);
        _image.WidthRequest = paperWidth;
        _image.HeightRequest = paperHeight;
    }

    private static void LockSize(VisualElement view, double width, double height)
    {
        view.WidthRequest = width;
        view.HeightRequest = height;
        view.MinimumWidthRequest = width;
        view.MinimumHeightRequest = height;
        view.MaximumWidthRequest = width;
        view.MaximumHeightRequest = height;
    }

    private void CancelRender()
    {
        _renderCancellation?.Cancel();
        _renderCancellation?.Dispose();
        _renderCancellation = null;
    }
}
