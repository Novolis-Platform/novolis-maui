using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
using Novolis.Pdf.Rendering;

namespace Novolis.Maui.PdfViewer;

/// <summary>Virtualized MAUI page cell that renders only while it is attached.</summary>
public sealed class PdfPageView : ContentView
{
    private readonly Image _image = new()
    {
        Aspect = Aspect.AspectFit,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Start,
    };
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
            propertyChanged: static (bindable, _, _) =>
            {
                var page = (PdfPageView)bindable;
                page.ApplySize();
                page.QueueRender();
            });

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

    /// <summary>Provides the viewer's bounded page rendering callback.</summary>
    public Func<int, CancellationToken, Task<PdfRenderedPage>>? RenderPageAsync { get; set; }

    /// <summary>Receives a render failure for the current cell.</summary>
    public Action<Exception>? RenderFailed { get; set; }

    /// <summary>Last painted PNG for the MAUI UI agent.</summary>
    public byte[]? TryGetLastFramePng() => _pngBytes is { Length: > 0 } ? _pngBytes : null;

    /// <summary>Creates a virtualized page cell.</summary>
    public PdfPageView()
    {
        Padding = new Thickness(8);
        BackgroundColor = Profile.Surface;
        Content = new Border
        {
            Stroke = new SolidColorBrush(Profile.Border),
            StrokeThickness = 1,
            Padding = new Thickness(4),
            Content = _image,
        };
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
            RenderFailed?.Invoke(exception);
        }
    }

    private void ApplySize()
    {
        if (PageWidth <= 0)
            return;
        WidthRequest = PageWidth;
        var imageWidth = Math.Max(80, PageWidth - 24);
        _image.WidthRequest = imageWidth;
        _image.HeightRequest = _pixelWidth > 0 && _pixelHeight > 0
            ? imageWidth * _pixelHeight / _pixelWidth
            : Math.Max(120, imageWidth * 1.414);
        HeightRequest = _image.HeightRequest + 24;
    }

    private void CancelRender()
    {
        _renderCancellation?.Cancel();
        _renderCancellation?.Dispose();
        _renderCancellation = null;
    }
}
