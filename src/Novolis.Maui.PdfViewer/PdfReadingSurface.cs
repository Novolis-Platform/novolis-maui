using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Novolis.Maui.GraphicalProfile;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.PdfViewer;

/// <summary>One camera over a stacked PDF. Visible pages are layers; pan and pinch move the camera.</summary>
public sealed class PdfReadingSurface : ContentView
{
    private const int LayerCount = 5;
    private const int CacheLimit = 8;
    private const double InertiaStopSpeed = 42;
    private readonly AbsoluteLayout _stage = new()
    {
        BackgroundColor = Profile.Border,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        IsClippedToBounds = true,
    };
    private readonly BoxView _gap = new()
    {
        Color = Profile.Border,
        InputTransparent = true,
    };
    private readonly Image[] _layers = new Image[LayerCount];
    private readonly int[] _layerPages = [-1, -1, -1, -1, -1];
    private readonly CacheKey[] _layerKeys = new CacheKey[LayerCount];
    private readonly Dictionary<CacheKey, ImageSource> _sources = [];
    private readonly Dictionary<CacheKey, string> _androidFiles = [];
    private readonly HashSet<CacheKey> _pending = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<(double Width, double Height)> _media = [];
    private PdfPageBox[] _boxes = [];
    private PdfParsedDocument? _document;
    private IReadOnlyList<PdfResolvedPage> _pages = [];
    private PdfSkiaPageRenderer? _renderer;
    private double _paneWidth = 360;
    private double _paneHeight = 640;
    private double _zoom = 1;
    private PdfFitKind _fit = PdfFitKind.Width;
    private int _rotation;
    private double _scrollY;
    private double _scrollX;
    private bool _pinching;
    private double _pinchSpan;
    private PointF _lastPoint;
    private PointF _tapStart;
    private DateTime _tapDown;
    private DateTime _lastTap;
    private DateTime _lastMoveAt;
    private double _velocityX;
    private double _velocityY;
    private bool _dragging;
    private CancellationTokenSource? _inertiaCancellation;
    private int _renderGeneration;
    private int _pageIndex;

    /// <summary>Creates the reading surface.</summary>
    public PdfReadingSurface()
    {
        AutomationId = "PdfReadingPage";
        BackgroundColor = Profile.Border;
        IsClippedToBounds = true;
        AbsoluteLayout.SetLayoutFlags(_gap, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_gap, new Rect(0, 0, 1, 1));
        _stage.Add(_gap);
        for (var i = 0; i < LayerCount; i++)
        {
            var image = new Image
            {
                Aspect = Aspect.Fill,
                BackgroundColor = Colors.White,
                IsVisible = false,
                InputTransparent = true,
            };
            AbsoluteLayout.SetLayoutFlags(image, AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(image, new Rect(0, 0, 1, 1));
            _layers[i] = image;
            _stage.Add(image);
        }

        _stage.InputTransparent = true;
        var touch = new GraphicsView
        {
            BackgroundColor = Colors.Transparent,
            Drawable = new PdfTouchLayer(),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = false,
        };
        touch.StartInteraction += OnTouchStart;
        touch.DragInteraction += OnTouchDrag;
        touch.EndInteraction += OnTouchEnd;
        touch.CancelInteraction += OnTouchCancel;
        var root = new Grid();
        root.Add(_stage);
        root.Add(touch);
        Content = root;
        SizeChanged += (_, _) =>
        {
            if (Width > 32 && Height > 32)
                SetPane(PdfViewerLayout.WidthDip(Width), PdfViewerLayout.HeightDip(Height));
        };
    }

    /// <summary>Raised when the page at the top of the viewport changes.</summary>
    public event EventHandler<int>? PageChanged;

    /// <summary>Raised when pinch or programmatic zoom changes the scale.</summary>
    public event EventHandler<double>? ZoomChanged;

    /// <summary>Current top page, zero-based.</summary>
    public int PageIndex => _pageIndex;

    /// <summary>Current zoom relative to Fit.</summary>
    public double Zoom => _zoom;

    /// <summary>Applies graphical-profile colors to the page stack gap.</summary>
    public void ApplyChrome()
    {
        BackgroundColor = Profile.Border;
        _stage.BackgroundColor = Profile.Border;
        _gap.Color = Profile.Border;
    }

    /// <summary>Binds a parsed document. Pass null to clear.</summary>
    public void Bind(
        PdfParsedDocument? document,
        IReadOnlyList<PdfResolvedPage> pages,
        PdfSkiaPageRenderer renderer)
    {
        StopInertia();
        ClearCache();
        _document = document;
        _pages = pages;
        _renderer = renderer;
        RefreshMedia();
        Relayout(keepPage: false);
        _scrollX = 0;
        Compose(rasterize: true);
    }

    /// <summary>Applies pane size, zoom, fit, and page rotation.</summary>
    public void SetView(double paneWidth, double paneHeight, double zoom, PdfFitKind fit, int rotation)
    {
        var nextRotation = ((rotation % 360) + 360) % 360;
        var mediaDirty = nextRotation != _rotation;
        _paneWidth = System.Math.Max(160, paneWidth);
        _paneHeight = System.Math.Max(160, paneHeight);
        _zoom = System.Math.Clamp(zoom, PdfZoomLevels.Minimum, PdfZoomLevels.Maximum);
        _fit = fit;
        if (mediaDirty)
        {
            _rotation = nextRotation;
            ClearCache();
            RefreshMedia();
        }

        Relayout(keepPage: true);
        Compose(rasterize: !_pinching);
    }

    /// <summary>Sets pane DIP without changing zoom.</summary>
    public void SetPane(double paneWidth, double paneHeight)
    {
        var width = System.Math.Max(160, paneWidth);
        var height = System.Math.Max(160, paneHeight);
        if (System.Math.Abs(width - _paneWidth) < 8 && System.Math.Abs(height - _paneHeight) < 8)
            return;
        if (System.Math.Abs(width - _paneWidth) >= 48 || System.Math.Abs(height - _paneHeight) >= 48)
            ClearCache();
        _paneWidth = width;
        _paneHeight = height;
        Relayout(keepPage: true);
        Compose(rasterize: !_pinching);
    }

    /// <summary>Scrolls so <paramref name="pageIndex"/> starts at the top.</summary>
    public void GoToPage(int pageIndex)
    {
        if (_boxes.Length == 0)
            return;
        var index = System.Math.Clamp(pageIndex, 0, _boxes.Length - 1);
        _scrollY = PdfDocumentStack.ClampScroll(_boxes[index].Top, DocumentHeight, ViewportHeight);
        RaisePage(index);
        Compose(rasterize: true);
    }

    /// <summary>Pans the document by DIP.</summary>
    public void ScrollBy(double deltaY) => ScrollBy(0, deltaY);

    /// <summary>Pans the document by DIP in both axes.</summary>
    public void ScrollBy(double deltaX, double deltaY)
        => ScrollByCore(deltaX, deltaY, rasterize: true);

    private bool ScrollByCore(double deltaX, double deltaY, bool rasterize)
    {
        if (_boxes.Length == 0)
            return false;
        var oldScrollX = _scrollX;
        var oldScrollY = _scrollY;
        var density = PdfViewerLayout.Density;
        if (density > 1.2 && System.Math.Abs(deltaY) > ViewportHeight)
            deltaY /= density;
        if (density > 1.2 && System.Math.Abs(deltaX) > ViewportWidth)
            deltaX /= density;
        var pageWidth = _boxes[System.Math.Clamp(_pageIndex, 0, _boxes.Length - 1)].Width;
        _scrollX = PdfDocumentStack.PageX(pageWidth, ViewportWidth, _scrollX - deltaX);
        _scrollY = PdfDocumentStack.ClampScroll(_scrollY + deltaY, DocumentHeight, ViewportHeight);
        RaisePage(PdfDocumentStack.PageAt(_boxes, _scrollY));
        var moved = System.Math.Abs(oldScrollY - _scrollY) > 0.01
            || System.Math.Abs(oldScrollX - _scrollX) > 0.01;
        if (moved)
            Compose(rasterize);
        return moved;
    }

    /// <summary>Zooms, keeping the viewport midpoint stable.</summary>
    public void SetZoom(double zoom) => ApplyZoom(
        zoom,
        ViewportWidth * 0.5,
        ViewportHeight * 0.5,
        rasterize: true);

    /// <summary>Applies an incremental pinch factor without re-rasterizing.</summary>
    public void ScaleBy(double factor)
        => ScaleBy(factor, ViewportWidth * 0.5, ViewportHeight * 0.5);

    /// <summary>Applies an incremental pinch factor around a viewport focus point.</summary>
    public void ScaleBy(double factor, double focusX, double focusY)
    {
        if (factor <= 0 || !double.IsFinite(factor))
            return;
        _pinching = true;
        ApplyZoom(_zoom * factor, focusX, focusY, rasterize: false);
    }

    /// <summary>Finishes a pinch and paints at the new scale.</summary>
    public void EndScale()
    {
        _pinching = false;
        _pinchSpan = 0;
        Compose(rasterize: true);
    }

    /// <summary>Drops bitmaps and the document.</summary>
    public void Clear()
    {
        StopInertia();
        ClearCache();
        _document = null;
        _pages = [];
        _media.Clear();
        _boxes = [];
        _scrollY = 0;
        _scrollX = 0;
        _pageIndex = 0;
        Compose(rasterize: false);
    }

    private double DocumentHeight => PdfDocumentStack.DocumentHeight(_boxes);

    private double ViewportWidth
    {
        get
        {
            var live = Width > 32 ? PdfViewerLayout.WidthDip(Width) : 0;
            return live > 32 ? live : _paneWidth;
        }
    }

    private double ViewportHeight
    {
        get
        {
            var live = Height > 32 ? PdfViewerLayout.HeightDip(Height) : 0;
            return live > 32 ? live : _paneHeight;
        }
    }

    private void RefreshMedia()
    {
        _media.Clear();
        foreach (var page in _pages)
        {
            var info = page.Info with { Rotation = page.Info.Rotation + _rotation };
            _media.Add((
                info.DisplayWidth > 0 ? info.DisplayWidth : 432,
                info.DisplayHeight > 0 ? info.DisplayHeight : 648));
        }
    }

    private void Relayout(bool keepPage)
    {
        var previous = _boxes;
        var previousScroll = _scrollY;
        var page = keepPage ? _pageIndex : 0;
        _boxes = PdfDocumentStack.Layout(
            _media,
            _paneWidth,
            _paneHeight,
            _zoom,
            _fit,
            PdfViewerLayout.MaxDisplayDip);
        if (_boxes.Length == 0)
        {
            _scrollY = 0;
            return;
        }

        if (keepPage && previous.Length == _boxes.Length)
        {
            _scrollY = PdfDocumentStack.ClampScroll(
                PdfDocumentStack.ScrollAfterZoom(previous, _boxes, previousScroll, ViewportHeight * 0.5),
                DocumentHeight,
                ViewportHeight);
            var index = System.Math.Clamp(_pageIndex, 0, _boxes.Length - 1);
            _scrollX = PdfDocumentStack.PageX(_boxes[index].Width, ViewportWidth, _scrollX);
            RaisePage(PdfDocumentStack.PageAt(_boxes, _scrollY));
            return;
        }

        page = System.Math.Clamp(page, 0, _boxes.Length - 1);
        _scrollY = PdfDocumentStack.ClampScroll(_boxes[page].Top, DocumentHeight, ViewportHeight);
        _scrollX = PdfDocumentStack.PageX(_boxes[page].Width, ViewportWidth, 0);
        RaisePage(page);
    }

    private void ApplyZoom(double zoom, double focusX, double focusY, bool rasterize)
    {
        zoom = System.Math.Clamp(zoom, PdfZoomLevels.Minimum, PdfZoomLevels.Maximum);
        if (System.Math.Abs(zoom - _zoom) < 0.001 && !rasterize)
            return;
        var before = _boxes;
        var beforeScrollX = _scrollX;
        var focusPage = before.Length == 0
            ? 0
            : PdfDocumentStack.PageAt(before, _scrollY + focusY);
        _zoom = zoom;
        var after = PdfDocumentStack.Layout(
            _media,
            _paneWidth,
            _paneHeight,
            _zoom,
            _fit,
            PdfViewerLayout.MaxDisplayDip);
        _scrollY = PdfDocumentStack.ClampScroll(
            PdfDocumentStack.ScrollAfterZoom(before, after, _scrollY, focusY),
            PdfDocumentStack.DocumentHeight(after),
            ViewportHeight);
        _boxes = after;
        if (_boxes.Length > 0)
        {
            var page = System.Math.Clamp(_pageIndex, 0, _boxes.Length - 1);
            if (focusPage < before.Length && focusPage < after.Length)
            {
                var old = before[focusPage];
                var next = after[focusPage];
                var oldLeft = PdfDocumentStack.PageX(old.Width, ViewportWidth, beforeScrollX);
                var fraction = old.Width <= 0
                    ? 0.5
                    : System.Math.Clamp((focusX - oldLeft) / old.Width, 0, 1);
                var desiredLeft = focusX - (fraction * next.Width);
                _scrollX = PageScrollForLeft(next.Width, ViewportWidth, desiredLeft);
            }
            else
            {
                _scrollX = PdfDocumentStack.PageX(
                    _boxes[page].Width,
                    ViewportWidth,
                    (ViewportWidth - _boxes[page].Width) * 0.5);
            }
        }
        RaisePage(PdfDocumentStack.PageAt(_boxes, _scrollY));
        ZoomChanged?.Invoke(this, _zoom);
        Compose(rasterize);
    }

    private static double PageScrollForLeft(double pageWidth, double viewportWidth, double left)
    {
        if (pageWidth <= viewportWidth)
            return 0;
        return System.Math.Clamp(left, viewportWidth - pageWidth, 0);
    }

    private void Compose(bool rasterize)
    {
        var visible = PdfDocumentStack.Visible(_boxes, _scrollY, ViewportHeight).ToArray();
        if (visible.Length > 0)
        {
            var first = System.Math.Max(0, visible[0].Index - 1);
            var last = System.Math.Min(_boxes.Length - 1, visible[^1].Index + 1);
            visible = _boxes[first..(last + 1)];
        }
        visible = visible.Take(LayerCount).ToArray();
        var assigned = new bool[LayerCount];
        foreach (var box in visible)
        {
            var slot = LayerFor(box.Index);
            if (slot < 0)
                continue;
            assigned[slot] = true;
            Place(slot, box, keepBitmap: true, rasterize);
        }

        foreach (var box in visible)
        {
            if (LayerFor(box.Index) >= 0)
                continue;
            var slot = -1;
            for (var i = 0; i < LayerCount; i++)
            {
                if (!assigned[i])
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
                break;
            assigned[slot] = true;
            _layers[slot].Source = null;
            _layerKeys[slot] = default;
            Place(slot, box, keepBitmap: false, rasterize);
        }

        for (var i = 0; i < LayerCount; i++)
        {
            if (assigned[i])
                continue;
            _layers[i].IsVisible = false;
            _layers[i].Source = null;
            _layerPages[i] = -1;
            _layerKeys[i] = default;
        }
    }

    private int LayerFor(int pageIndex)
    {
        for (var i = 0; i < LayerCount; i++)
        {
            if (_layerPages[i] == pageIndex)
                return i;
        }

        return -1;
    }

    private void Place(int slot, PdfPageBox box, bool keepBitmap, bool rasterize)
    {
        var image = _layers[slot];
        var width = System.Math.Clamp(box.Width, 80, PdfViewerLayout.MaxDisplayDip);
        var height = System.Math.Clamp(box.Height, 80, PdfViewerLayout.MaxDisplayDip);
        var x = PdfDocumentStack.PageX(width, ViewportWidth, _scrollX);
        var y = box.Top - _scrollY;
        image.IsVisible = true;
        image.WidthRequest = width;
        image.HeightRequest = height;
        image.TranslationX = 0;
        image.TranslationY = 0;
        AbsoluteLayout.SetLayoutBounds(image, new Rect(x, y, width, height));
        var pixels = PdfViewerLayout.PixelSize(width, height);
        var key = new CacheKey(box.Index, pixels.Width, _rotation);
        _layerPages[slot] = box.Index;
        if (_sources.TryGetValue(key, out var source))
        {
            if (!ReferenceEquals(image.Source, source))
                image.Source = source;
            _layerKeys[slot] = key;
            return;
        }

        if (!keepBitmap)
        {
            image.Source = null;
            image.BackgroundColor = Colors.White;
            _layerKeys[slot] = default;
        }

        if (rasterize)
            QueueRender(key, box);
    }

    private void QueueRender(CacheKey key, PdfPageBox box)
    {
        if (_document is null || _renderer is null || key.Page < 0 || key.Page >= _pages.Count)
            return;
        lock (_pending)
        {
            if (!_pending.Add(key))
                return;
        }

        var document = _document;
        var page = _pages[key.Page];
        var renderer = _renderer;
        var generation = _renderGeneration;
        var width = key.Width;
        var height = System.Math.Max(1, (int)System.Math.Round(box.Height / System.Math.Max(1, box.Width) * width));
        height = System.Math.Min(height, PdfViewerLayout.MaxRasterEdge);
        _ = Task.Run(async () =>
        {
            try
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    var rendered = renderer.RenderScaled(document, page, width, height, key.Rotation);
                    var source = CreateSource(key, rendered.PngBytes, generation);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (generation != _renderGeneration || !ReferenceEquals(document, _document))
                        {
                            lock (_pending)
                                _pending.Remove(key);
                            return;
                        }
                        Store(key, source);
                        Compose(rasterize: false);
                    });
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch
            {
                lock (_pending)
                    _pending.Remove(key);
            }
        });
    }

    private ImageSource CreateSource(CacheKey key, byte[] png, int generation)
    {
        if (OperatingSystem.IsAndroid())
        {
            var cacheRoot = FileSystem.CacheDirectory;
            if (string.IsNullOrWhiteSpace(cacheRoot))
                cacheRoot = Path.GetTempPath();
            Directory.CreateDirectory(cacheRoot);
            var path = Path.Combine(
                cacheRoot,
                $"novolis-pdf-page-g{generation}-{key.Page}-{key.Width}-{key.Rotation}.png");
            File.WriteAllBytes(path, png);
            _androidFiles[key] = path;
            return new FileImageSource { File = path };
        }

        var copy = png;
        return ImageSource.FromStream(() => new MemoryStream(copy, writable: false));
    }

    private void Store(CacheKey key, ImageSource source)
    {
        lock (_pending)
            _pending.Remove(key);
        _sources[key] = source;
        while (_sources.Count > CacheLimit)
        {
            CacheKey evict = default;
            var found = false;
            var farthest = -1;
            foreach (var existing in _sources.Keys)
            {
                var distance = System.Math.Abs(existing.Page - _pageIndex);
                if (distance <= 0)
                    continue;
                if (distance > farthest)
                {
                    farthest = distance;
                    evict = existing;
                    found = true;
                }
            }

            if (!found)
                break;
            _sources.Remove(evict);
            if (_androidFiles.Remove(evict, out var path))
                _ = DeleteLaterAsync(path);
        }
    }

    private void OnTouchStart(object? sender, TouchEventArgs args)
    {
        StopInertia();
        var touches = args.Touches;
        if (touches.Length >= 2)
        {
            _pinching = true;
            _dragging = false;
            _velocityX = 0;
            _velocityY = 0;
            _pinchSpan = Span(touches);
            return;
        }

        if (touches.Length == 1)
        {
            _lastPoint = touches[0];
            _tapStart = touches[0];
            _tapDown = DateTime.UtcNow;
            _lastMoveAt = _tapDown;
            _velocityX = 0;
            _velocityY = 0;
            _dragging = true;
        }
    }

    private void OnTouchDrag(object? sender, TouchEventArgs args)
    {
        var touches = args.Touches;
        if (touches.Length >= 2)
        {
            var span = Span(touches);
            if (_pinchSpan > 8 && span > 8)
            {
                var focus = Midpoint(touches);
                ScaleBy(span / _pinchSpan, focus.X, focus.Y);
            }
            _pinchSpan = span;
            _pinching = true;
            return;
        }

        if (_pinching || touches.Length != 1)
            return;

        var point = touches[0];
        var now = DateTime.UtcNow;
        var elapsed = System.Math.Max(0.008, (now - _lastMoveAt).TotalSeconds);
        var deltaX = _lastPoint.X - point.X;
        var deltaY = _lastPoint.Y - point.Y;
        ScrollByCore(deltaX, deltaY, rasterize: false);
        var sampleX = deltaX / elapsed;
        var sampleY = deltaY / elapsed;
        _velocityX = (_velocityX * 0.65) + (sampleX * 0.35);
        _velocityY = (_velocityY * 0.65) + (sampleY * 0.35);
        _lastPoint = point;
        _lastMoveAt = now;
    }

    private void OnTouchEnd(object? sender, TouchEventArgs args)
    {
        if (_pinching)
        {
            EndScale();
            _dragging = false;
            return;
        }

        var touches = args.Touches;
        if (!_dragging)
            return;

        var point = touches.Length == 1 ? touches[0] : _lastPoint;
        var moved = Distance(point, _tapStart);
        var held = DateTime.UtcNow - _tapDown;
        _dragging = false;
        if (moved > 16)
        {
            if (System.Math.Sqrt((_velocityX * _velocityX) + (_velocityY * _velocityY)) > 80)
                StartInertia(_velocityX, _velocityY);
            Compose(rasterize: true);
            return;
        }

        if (held > TimeSpan.FromMilliseconds(400))
            return;

        var now = DateTime.UtcNow;
        if (now - _lastTap < TimeSpan.FromMilliseconds(350))
        {
            ApplyZoom(
                _zoom < 1.75 ? 2.5 : 1,
                point.X,
                point.Y,
                rasterize: true);
            _lastTap = DateTime.MinValue;
            return;
        }

        _lastTap = now;
    }

    private void OnTouchCancel(object? sender, EventArgs args)
    {
        if (_pinching)
            EndScale();
        _dragging = false;
    }

    private void StartInertia(double velocityX, double velocityY)
    {
        StopInertia();
        var cancellation = new CancellationTokenSource();
        _inertiaCancellation = cancellation;
        _ = RunInertiaAsync(velocityX, velocityY, cancellation);
    }

    private async Task RunInertiaAsync(
        double velocityX,
        double velocityY,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        var previous = DateTime.UtcNow;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(16, token).ConfigureAwait(false);
                var now = DateTime.UtcNow;
                var elapsed = System.Math.Clamp((now - previous).TotalSeconds, 0.008, 0.05);
                previous = now;
                var moved = await MainThread.InvokeOnMainThreadAsync(
                        () => ScrollByCore(
                            velocityX * elapsed,
                            velocityY * elapsed,
                            rasterize: false))
                    .ConfigureAwait(false);
                if (!moved)
                    break;

                var friction = System.Math.Exp(-5.2 * elapsed);
                velocityX *= friction;
                velocityY *= friction;
                if (System.Math.Sqrt((velocityX * velocityX) + (velocityY * velocityY)) < InertiaStopSpeed)
                    break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!ReferenceEquals(_inertiaCancellation, cancellation))
                        return;
                    _inertiaCancellation = null;
                    Compose(rasterize: true);
                }).ConfigureAwait(false);
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }

    private void StopInertia()
    {
        if (_inertiaCancellation is not { } cancellation)
            return;
        _inertiaCancellation = null;
        cancellation.Cancel();
    }

    private static float Span(PointF[] touches)
    {
        if (touches.Length < 2)
            return 0;
        var dx = touches[0].X - touches[1].X;
        var dy = touches[0].Y - touches[1].Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static PointF Midpoint(PointF[] touches) =>
        touches.Length < 2
            ? touches.FirstOrDefault()
            : new PointF(
                (touches[0].X + touches[1].X) / 2,
                (touches[0].Y + touches[1].Y) / 2);

    private static float Distance(PointF a, PointF b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private void RaisePage(int index)
    {
        if (index == _pageIndex)
            return;
        _pageIndex = index;
        PageChanged?.Invoke(this, index);
    }

    private void ClearCache()
    {
        _renderGeneration++;
        _sources.Clear();
        foreach (var path in _androidFiles.Values)
            _ = DeleteLaterAsync(path);
        _androidFiles.Clear();
        lock (_pending)
            _pending.Clear();
        for (var i = 0; i < LayerCount; i++)
        {
            _layers[i].Source = null;
            _layers[i].IsVisible = false;
            _layerPages[i] = -1;
            _layerKeys[i] = default;
        }
    }

    private static async Task DeleteLaterAsync(string path)
    {
        try
        {
            await Task.Delay(2000).ConfigureAwait(false);
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private readonly record struct CacheKey(int Page, int Width, int Rotation);
}
