using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;
using GraphicsImage = Microsoft.Maui.Graphics.IImage;

namespace Novolis.Maui.Map;

/// <summary>
/// Provider-neutral MAUI map view with raster tiles, geographic overlays,
/// selection, pan, pinch, and inertial movement.
/// </summary>
public sealed class MapView : GraphicsView, IDrawable
{
    const int MaximumCachedTiles = 128;
    const int MaximumConcurrentTileRequests = 6;
    static readonly TimeSpan TileRefreshDebounce = TimeSpan.FromMilliseconds(90);

    readonly Dictionary<MapTileKey, GraphicsImage> _tiles = new();
    readonly Dictionary<MapTileKey, long> _tileLastUsed = new();
    readonly HashSet<MapTileKey> _staleTiles = new();
    readonly SemaphoreSlim _tileRequestGate = new(MaximumConcurrentTileRequests);
    readonly List<GeoCoordinate> _drawingPoints = [];
    CancellationTokenSource? _tileRefreshCancellation;
    CancellationTokenSource? _refreshScheduleCancellation;
    Task? _refreshScheduleTask;
    CancellationTokenSource? _inertiaCancellation;
    long _tileRefreshGeneration;
    long _refreshRevision;
    long _tileUseCounter;
    bool _refreshImmediateRequested;
    bool _tileLoadingEnabled = true;
    int _interactionDepth;
    MapViewport _panStartViewport;
    MapViewport _pinchStartViewport;
    GeoCoordinate _pinchAnchor;
    double _pinchScreenX;
    double _pinchScreenY;
    double _panLastX;
    double _panLastY;
    DateTimeOffset _panLastAt;
    double _panVelocityX;
    double _panVelocityY;
    bool _panActive;
    bool _pinchActive;
    GeoDrawingKind? _drawingKind;
    GeoCoordinate? _selectedCoordinate;

    /// <summary>Creates a map view with a neutral initial viewport.</summary>
    public MapView()
    {
        Drawable = this;
        BackgroundColor = Colors.LightGray;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        AutomationId = "MapView";
        SemanticProperties.SetDescription(
            this,
            "Interactive map. Pan to move, pinch to zoom, and tap to select a location.");
        SemanticProperties.SetHint(this, "Map");

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTapped;
        GestureRecognizers.Add(doubleTap);

        SizeChanged += (_, _) => QueueTileRefresh();
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        if (Handler is null)
        {
            StopRefreshSchedule();
            _tileRefreshCancellation?.Cancel();
            CancelInertia();
            ClearTiles();
            IsLoading = false;
        }
        else
        {
            QueueTileRefresh(immediate: true);
        }

        base.OnHandlerChanged();
    }

    /// <summary>Camera state.</summary>
    public MapViewport Viewport
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            InvalidateMap();
            QueueTileRefresh();
        }
    } = new(new GeoCoordinate(0, 0), 2);

    /// <summary>Encoded tile source used by the view.</summary>
    public IMapRasterSource? TileSource
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;

            field = value;
            Interlocked.Increment(ref _tileRefreshGeneration);
            _tileRefreshCancellation?.Cancel();
            ClearTiles();
            QueueTileRefresh(immediate: true);
            InvalidateMap();
        }
    }

    /// <summary>Markers rendered over the map.</summary>
    public IReadOnlyList<MapMarker>? Markers
    {
        get;
        set
        {
            field = value;
            ReconcileOverlaySelection();
            InvalidateMap();
        }
    }

    /// <summary>Circles rendered over the map.</summary>
    public IReadOnlyList<MapCircleOverlay>? Circles
    {
        get;
        set
        {
            field = value;
            ReconcileOverlaySelection();
            InvalidateMap();
        }
    }

    /// <summary>Polygons rendered over the map.</summary>
    public IReadOnlyList<MapPolygonOverlay>? Polygons
    {
        get;
        set
        {
            field = value;
            ReconcileOverlaySelection();
            InvalidateMap();
        }
    }

    /// <summary>Connected geographic tracks rendered below markers.</summary>
    public IReadOnlyList<MapTrackOverlay>? Tracks
    {
        get;
        set
        {
            field = value;
            ReconcileOverlaySelection();
            InvalidateMap();
        }
    }

    /// <summary>Optional capabilities enabled by the host.</summary>
    public MapInteractionOptions InteractionOptions { get; set; } =
        MapInteractionOptions.Disabled;

    /// <summary>Optional host-provided clipboard writer used by tests or product policy.</summary>
    public Func<string, CancellationToken, Task>? ClipboardWriter { get; set; }

    /// <summary>Deterministic work and resource counters for diagnostics and tests.</summary>
    public MapPerformanceCounters PerformanceCounters { get; } = new();

    /// <summary>
    /// Optional encoded-tile decoder. Hosts and tests can provide a decoder to
    /// control native image creation and release policy.
    /// </summary>
    public Func<MapRasterTile, GraphicsImage?>? TileDecoder { get; set; }

    /// <summary>Currently selected marker, if the selected coordinate came from one.</summary>
    public MapMarker? SelectedMarker { get; private set; }

    /// <summary>Currently selected typed overlay, when selection came from an overlay.</summary>
    public MapOverlayKey? SelectedOverlay { get; private set; }

    /// <summary>Active drawing mode, or null when the map is not drawing.</summary>
    public GeoDrawingKind? ActiveDrawingKind => _drawingKind;

    /// <summary>Whether a host-started drawing session is active.</summary>
    public bool IsDrawing => _drawingKind is not null;

    /// <summary>Vertices collected by the active drawing session.</summary>
    public IReadOnlyList<GeoCoordinate> DrawingPoints => _drawingPoints;

    /// <summary>Selected geographic coordinate, if any.</summary>
    public GeoCoordinate? SelectedCoordinate
    {
        get => _selectedCoordinate;
        set
        {
            var marker = value is { } coordinate
                ? Markers?.FirstOrDefault(item => item.Position == coordinate)
                : null;
            SetSelection(
                value,
                marker,
                marker is null ? null : MarkerKey(marker));
        }
    }

    /// <summary>Selects a coordinate as a user-facing map interaction.</summary>
    public void SelectCoordinate(GeoCoordinate coordinate)
    {
        var marker = Markers?.FirstOrDefault(item => item.Position == coordinate);
        ApplyInteractionSelection(
            coordinate,
            marker,
            marker is null ? null : MarkerKey(marker));
    }

    /// <summary>Selects a marker and retains its metadata and host tag.</summary>
    public void SelectMarker(MapMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        ApplyInteractionSelection(marker.Position, marker, MarkerKey(marker));
    }

    /// <summary>Selects a host-owned overlay by type-qualified identity.</summary>
    public bool SelectOverlay(MapOverlayKey key)
    {
        if (!TryResolveOverlay(key, out var coordinate, out var marker))
        {
            ClearSelection();
            return false;
        }

        ApplyInteractionSelection(coordinate, marker, key);
        return true;
    }

    /// <summary>Clears any selected coordinate and overlay.</summary>
    public void ClearSelection() => SetSelection(null, null, null);

    /// <summary>Requests removal of the selected overlay without mutating host collections.</summary>
    public bool RequestEraseSelectedOverlay()
    {
        if (SelectedOverlay is not { } key)
            return false;

        OverlayEraseRequested?.Invoke(key);
        return true;
    }

    /// <summary>Optional attribution override shown in the lower-left corner.</summary>
    public string? Attribution
    {
        get;
        set
        {
            field = value;
            InvalidateMap();
        }
    }

    /// <summary>Whether visible tiles are currently loading.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets or sets whether this view may fetch tiles. Existing tiles remain
    /// visible while loading is disabled.
    /// </summary>
    public bool TileLoadingEnabled
    {
        get => _tileLoadingEnabled;
        set
        {
            if (_tileLoadingEnabled == value)
                return;

            _tileLoadingEnabled = value;
            if (!value)
            {
                StopRefreshSchedule();
                _tileRefreshCancellation?.Cancel();
                IsLoading = false;
                InvalidateMap();
                return;
            }

            QueueTileRefresh(immediate: true);
        }
    }

    /// <summary>Recoverable provider error displayed over the map.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Whether one or more visible tiles came from a stale cache fallback.</summary>
    public bool HasStaleTiles =>
        HasValidSize()
        && GetVisibleTileKeys().Any(_staleTiles.Contains);

    /// <summary>Raised when the user selects a geographic point.</summary>
    public event Action<GeoCoordinate>? PointSelected;

    /// <summary>Raised when the user selects a marker.</summary>
    public event Action<MapMarker>? MarkerSelected;

    /// <summary>Raised when a drawing session is completed.</summary>
    public event Action<GeoDrawing>? DrawingCompleted;

    /// <summary>Raised when the selected marker or coordinate changes.</summary>
    public event Action? SelectionChanged;

    /// <summary>Raised when the selected overlay changes, including a clear selection.</summary>
    public event Action<MapOverlayKey?>? OverlaySelectionChanged;

    /// <summary>Raised when a selectable overlay is chosen by the user or host.</summary>
    public event Action<MapOverlayKey>? OverlaySelected;

    /// <summary>Requests that the host remove a selected overlay from its source collection.</summary>
    public event Action<MapOverlayKey>? OverlayEraseRequested;

    /// <summary>Sets the camera center and zoom.</summary>
    public void SetViewport(GeoCoordinate center, double zoom) =>
        Viewport = new MapViewport(center, zoom);

    /// <summary>Returns exactly the tile keys intersecting the current viewport.</summary>
    public IReadOnlyList<MapTileKey> GetVisibleTileKeys()
    {
        PerformanceCounters.RecordVisibleTileCalculation();
        return HasValidSize()
            ? WebMercatorTiles.VisibleTiles(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height)
            : [];
    }

    /// <summary>Copies the selected coordinate using the configured host clipboard.</summary>
    public Task<bool> CopySelectedCoordinateAsync(
        CancellationToken cancellationToken = default) =>
        CopySelectionAsync(
            SelectedMarker is { } marker
                ? GeoCoordinateText.Format(marker.Position)
                : SelectedCoordinate is { } coordinate
                    ? GeoCoordinateText.Format(coordinate)
                    : null,
            cancellationToken);

    /// <summary>Copies the selected coordinate and marker identity as JSON.</summary>
    public Task<bool> CopySelectedJsonAsync(
        CancellationToken cancellationToken = default) =>
        CopySelectionAsync(
            SelectedCoordinate is not { } coordinate
                ? null
                : GeoCoordinateText.ToJson(
                    coordinate,
                    SelectedMarker?.Id,
                    SelectedMarker?.Label,
                    SelectedMarker?.Metadata),
            cancellationToken);

    /// <summary>Executes a host-mapped keyboard command.</summary>
    public async Task<bool> ExecuteKeyboardCommandAsync(
        MapKeyboardCommand command,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case MapKeyboardCommand.CopyCoordinate:
                if (!InteractionOptions.EnableClipboardShortcuts)
                    return false;
                return await CopySelectedCoordinateAsync(cancellationToken);
            case MapKeyboardCommand.CopyJson:
                if (!InteractionOptions.EnableClipboardShortcuts)
                    return false;
                return await CopySelectedJsonAsync(cancellationToken);
            case MapKeyboardCommand.ZoomIn:
                if (!InteractionOptions.EnableKeyboardNavigation)
                    return false;
                ZoomAt(Width / 2, Height / 2, Viewport.Zoom + 1);
                return true;
            case MapKeyboardCommand.ZoomOut:
                if (!InteractionOptions.EnableKeyboardNavigation)
                    return false;
                ZoomAt(Width / 2, Height / 2, Viewport.Zoom - 1);
                return true;
            case MapKeyboardCommand.PanLeft:
                if (!InteractionOptions.EnableKeyboardNavigation)
                    return false;
                PanBy(80, 0);
                return true;
            case MapKeyboardCommand.PanRight:
                if (!InteractionOptions.EnableKeyboardNavigation)
                    return false;
                PanBy(-80, 0);
                return true;
            case MapKeyboardCommand.PanUp:
                if (!InteractionOptions.EnableKeyboardNavigation)
                    return false;
                PanBy(0, 80);
                return true;
            case MapKeyboardCommand.PanDown:
                if (!InteractionOptions.EnableKeyboardNavigation)
                    return false;
                PanBy(0, -80);
                return true;
            case MapKeyboardCommand.CompleteDrawing:
                if (!InteractionOptions.EnableDrawing)
                    return false;
                return CompleteDrawing() is not null;
            case MapKeyboardCommand.CancelDrawing:
                if (!InteractionOptions.EnableDrawing || !IsDrawing)
                    return false;
                CancelDrawing();
                return true;
            case MapKeyboardCommand.EraseSelectedOverlay:
                if (!InteractionOptions.EnableOverlayErasure)
                    return false;
                return RequestEraseSelectedOverlay();
            default:
                return false;
        }
    }

    /// <summary>Starts a host-enabled geographic drawing session.</summary>
    public bool BeginDrawing(GeoDrawingKind kind)
    {
        if (!InteractionOptions.EnableDrawing)
            return false;

        _drawingKind = kind;
        _drawingPoints.Clear();
        InvalidateMap();
        return true;
    }

    /// <summary>Adds a vertex to the active drawing session.</summary>
    public bool AddDrawingPoint(GeoCoordinate coordinate)
    {
        if (_drawingKind is not { } kind)
            return false;

        if ((kind is GeoDrawingKind.Circle or GeoDrawingKind.Rectangle)
            && _drawingPoints.Count >= 1)
        {
            if (_drawingPoints.Count == 1)
                _drawingPoints.Add(coordinate);
            else
                _drawingPoints[1] = coordinate;
        }
        else
        {
            _drawingPoints.Add(coordinate);
        }
        InvalidateMap();
        if (kind == GeoDrawingKind.Point)
            CompleteDrawing();
        return true;
    }

    /// <summary>
    /// Routes a platform-hosted screen tap through the same selection and drawing
    /// path as the MAUI gesture recognizer.
    /// </summary>
    public bool HandleScreenTap(double screenX, double screenY)
    {
        if (!HasValidSize())
            return false;

        if (IsDrawing)
        {
            return AddDrawingPoint(
                WebMercatorTiles.PixelToGeo(
                    Viewport.Center,
                    Viewport.Zoom,
                    Width,
                    Height,
                    screenX,
                    screenY));
        }

        var hit = HitTestOverlay(screenX, screenY);
        if (hit is { } overlay)
            return SelectOverlay(overlay.Key);

        SelectCoordinate(
            WebMercatorTiles.PixelToGeo(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                screenX,
                screenY));
        return true;
    }

    /// <summary>Completes the active drawing and raises <see cref="DrawingCompleted"/>.</summary>
    public GeoDrawing? CompleteDrawing()
    {
        if (_drawingKind is not { } kind
            || !HasEnoughDrawingPoints(kind, _drawingPoints.Count))
        {
            return null;
        }

        var drawing = new GeoDrawing(kind, _drawingPoints);
        _drawingKind = null;
        _drawingPoints.Clear();
        InvalidateMap();
        DrawingCompleted?.Invoke(drawing);
        return drawing;
    }

    /// <summary>Cancels the active drawing without raising completion.</summary>
    public void CancelDrawing()
    {
        if (_drawingKind is null)
            return;

        _drawingKind = null;
        _drawingPoints.Clear();
        InvalidateMap();
    }

    /// <summary>Zooms around a screen point while retaining its geographic anchor.</summary>
    public void ZoomAt(double screenX, double screenY, double zoom)
    {
        if (!HasValidSize())
            return;

        var clampedZoom = global::System.Math.Clamp(
            zoom,
            MapViewport.MinimumZoom,
            MapViewport.MaximumZoom);
        var anchor = WebMercatorTiles.PixelToGeo(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            screenX,
            screenY);
        var center = WebMercatorTiles.CenterForAnchor(
            clampedZoom,
            Width,
            Height,
            anchor,
            screenX,
            screenY);
        Viewport = new MapViewport(center, clampedZoom);
    }

    /// <summary>Translates the viewport by a screen-pixel delta.</summary>
    public void PanBy(double deltaX, double deltaY)
    {
        if (!HasValidSize())
            return;

        var center = WebMercatorTiles.PixelToGeo(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            Width / 2 - deltaX,
            Height / 2 - deltaY);
        Viewport = new MapViewport(center, Viewport.Zoom);
    }

    /// <summary>Defers network refreshes while a continuous camera gesture is active.</summary>
    public void BeginCameraInteraction()
    {
        Interlocked.Increment(ref _interactionDepth);
        CancelInertia();
    }

    /// <summary>Ends a continuous camera gesture and refreshes the final viewport.</summary>
    public void EndCameraInteraction()
    {
        var depth = Interlocked.Decrement(ref _interactionDepth);
        if (depth > 0)
            return;

        Interlocked.Exchange(ref _interactionDepth, 0);
        QueueTileRefresh(immediate: true);
    }

    /// <summary>Requests an immediate tile refresh after a provider or network failure.</summary>
    public void RetryTiles()
    {
        ErrorMessage = null;
        InvalidateMap();
        QueueTileRefresh(immediate: true);
    }

    /// <summary>Releases decoded tile images currently held by this view.</summary>
    public void ClearTiles()
    {
        Interlocked.Increment(ref _tileRefreshGeneration);
        _tileRefreshCancellation?.Cancel();
        ErrorMessage = null;
        DisposeTiles();
        InvalidateMap();
    }

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.LightGray;
        canvas.FillRectangle(dirtyRect);
        if (!HasValidSize())
            return;

        DrawGrid(canvas);
        DrawTiles(canvas);
        DrawCircles(canvas);
        DrawPolygons(canvas);
        DrawTracks(canvas);
        DrawMarkers(canvas);
        DrawSelectedCoordinate(canvas);
        DrawDrawingPreview(canvas);
        DrawDrawingStatus(canvas);
        DrawStatus(canvas);
        DrawAttribution(canvas);
    }

    void DrawGrid(ICanvas canvas)
    {
        canvas.StrokeColor = Colors.DarkSlateGray.WithAlpha(0.18f);
        canvas.StrokeSize = 1;
        var spacing = global::System.Math.Max(
            32,
            Width / global::System.Math.Pow(2, Viewport.Zoom));
        for (var x = 0d; x < Width; x += spacing)
            canvas.DrawLine((float)x, 0, (float)x, (float)Height);
        for (var y = 0d; y < Height; y += spacing)
            canvas.DrawLine(0, (float)y, (float)Width, (float)y);
    }

    void DrawTiles(ICanvas canvas)
    {
        var visibleKeys = GetVisibleTileKeys();
        var drawn = new HashSet<MapTileKey>();

        foreach (var key in visibleKeys)
        {
            if (_tiles.ContainsKey(key))
                continue;

            for (var zoom = key.Zoom - 1; zoom >= 0; zoom--)
            {
                var scale = 1 << (key.Zoom - zoom);
                var parent = new MapTileKey(
                    zoom,
                    key.X / scale,
                    key.Y / scale);
                if (_tiles.ContainsKey(parent))
                {
                    if (drawn.Add(parent))
                        DrawTile(canvas, parent);
                    break;
                }
            }
        }

        foreach (var key in visibleKeys)
        {
            if (!_tiles.ContainsKey(key))
                continue;

            DrawTile(canvas, key);
        }
    }

    void DrawTile(ICanvas canvas, MapTileKey key)
    {
        if (!_tiles.TryGetValue(key, out var image))
            return;

        var rectangle = WebMercatorTiles.TilePixelRect(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            key);
        _tileLastUsed[key] = ++_tileUseCounter;
        canvas.DrawImage(
            image,
            (float)rectangle.X,
            (float)rectangle.Y,
            (float)rectangle.Width,
            (float)rectangle.Height);
    }

    void DrawCircles(ICanvas canvas)
    {
        if (Circles is not { Count: > 0 })
            return;

        var worldPixels = WebMercatorTiles.TileSizePixels
            * global::System.Math.Pow(2, Viewport.Zoom);
        foreach (var overlay in Circles)
        {
            var center = WebMercatorTiles.GeoToPixel(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                overlay.Circle.Center);
            var latitudeRadians = overlay.Circle.Center.Latitude
                * global::System.Math.PI
                / 180d;
            var metersPerPixel = 2
                * global::System.Math.PI
                * GeoDistance.MeanEarthRadiusMeters
                * global::System.Math.Max(0.01, global::System.Math.Cos(latitudeRadians))
                / worldPixels;
            var radius = overlay.Circle.RadiusMeters / metersPerPixel;

            var selected = IsSelected(MapOverlayKind.Circle, overlay.Id);
            var ink = overlay.Ink ?? Colors.DarkSlateBlue;
            canvas.FillColor = ink.WithAlpha(0.18f);
            canvas.FillCircle((float)center.X, (float)center.Y, (float)radius);
            canvas.StrokeColor = selected ? Colors.Gold : ink;
            canvas.StrokeSize = selected ? 3 : 2;
            canvas.DrawCircle((float)center.X, (float)center.Y, (float)radius);
            DrawLabel(canvas, overlay.Label, center.X, center.Y);
        }
    }

    void DrawTracks(ICanvas canvas)
    {
        if (Tracks is not { Count: > 0 })
            return;

        foreach (var track in Tracks)
        {
            if (track.Points.Count < 2)
                continue;

            var selected = IsSelected(MapOverlayKind.Track, track.Id);
            var pixels = WebMercatorTiles.GeoPathToPixels(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                track.Points);
            for (var index = 1; index < pixels.Count; index++)
            {
                var amount = pixels.Count <= 2
                    ? 0
                    : (index - 1) / (double)(pixels.Count - 2);
                canvas.StrokeColor = selected ? Colors.Gold : TrackInk(track, amount);
                canvas.StrokeSize = selected ? 4 : 3;
                canvas.DrawLine(
                    (float)pixels[index - 1].X,
                    (float)pixels[index - 1].Y,
                    (float)pixels[index].X,
                    (float)pixels[index].Y);
            }

            DrawLabel(
                canvas,
                track.Label,
                pixels[pixels.Count / 2].X,
                pixels[pixels.Count / 2].Y);
        }
    }

    void DrawPolygons(ICanvas canvas)
    {
        if (Polygons is not { Count: > 0 })
            return;

        foreach (var polygon in Polygons)
        {
            if (polygon.Points.Count < 2)
                continue;

            var selected = IsSelected(MapOverlayKind.Polygon, polygon.Id);
            var pixels = WebMercatorTiles.GeoPathToPixels(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                polygon.Points);
            if (polygon.Fill is { } fill && polygon.Points.Count >= 3)
            {
                var path = new PathF();
                path.MoveTo((float)pixels[0].X, (float)pixels[0].Y);
                for (var index = 1; index < pixels.Count; index++)
                {
                    path.LineTo((float)pixels[index].X, (float)pixels[index].Y);
                }

                path.Close();
                canvas.FillColor = fill;
                canvas.FillPath(path);
            }

            canvas.StrokeColor = selected ? Colors.Gold : polygon.Ink ?? Colors.DarkSlateBlue;
            canvas.StrokeSize = selected ? 3 : 2;
            for (var index = 1; index < pixels.Count; index++)
            {
                canvas.DrawLine(
                    (float)pixels[index - 1].X,
                    (float)pixels[index - 1].Y,
                    (float)pixels[index].X,
                    (float)pixels[index].Y);
            }

            if (polygon.Points[0] != polygon.Points[^1])
            {
                canvas.DrawLine(
                    (float)pixels[^1].X,
                    (float)pixels[^1].Y,
                    (float)pixels[0].X,
                    (float)pixels[0].Y);
            }

            DrawLabel(
                canvas,
                polygon.Label,
                pixels.Average(point => point.X),
                pixels.Average(point => point.Y));
        }
    }

    void DrawDrawingPreview(ICanvas canvas)
    {
        if (_drawingKind is not { } kind || _drawingPoints.Count == 0)
            return;

        canvas.StrokeColor = Colors.DarkOrange;
        canvas.StrokeSize = 3;
        if (kind == GeoDrawingKind.Circle && _drawingPoints.Count >= 2)
        {
            var center = WebMercatorTiles.GeoToPixel(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                _drawingPoints[0]);
            var edge = WebMercatorTiles.GeoToPixel(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                _drawingPoints[1]);
            var radius = global::System.Math.Sqrt(
                global::System.Math.Pow(edge.X - center.X, 2)
                + global::System.Math.Pow(edge.Y - center.Y, 2));
            canvas.DrawCircle((float)center.X, (float)center.Y, (float)radius);
            return;
        }

        if (kind == GeoDrawingKind.Rectangle && _drawingPoints.Count >= 2)
        {
            var points = new GeoRectangle(
                    _drawingPoints[0],
                    _drawingPoints[1])
                .ClosedCorners;
            var rectanglePixels = WebMercatorTiles.GeoPathToPixels(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                points);
            for (var index = 1; index < rectanglePixels.Count; index++)
            {
                canvas.DrawLine(
                    (float)rectanglePixels[index - 1].X,
                    (float)rectanglePixels[index - 1].Y,
                    (float)rectanglePixels[index].X,
                    (float)rectanglePixels[index].Y);
            }

            return;
        }

        var pixels = WebMercatorTiles.GeoPathToPixels(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            _drawingPoints);
        for (var index = 1; index < pixels.Count; index++)
        {
            canvas.DrawLine(
                (float)pixels[index - 1].X,
                (float)pixels[index - 1].Y,
                (float)pixels[index].X,
                (float)pixels[index].Y);
        }

        if (kind == GeoDrawingKind.Polygon && pixels.Count >= 3)
        {
            canvas.DrawLine(
                (float)pixels[^1].X,
                (float)pixels[^1].Y,
                (float)pixels[0].X,
                (float)pixels[0].Y);
        }
    }

    static Color TrackInk(MapTrackOverlay track, double amount)
    {
        if (track.FromInk is not { } from)
            return Colors.DarkSlateBlue;
        if (track.ToInk is not { } to)
            return from;

        var clamped = (float)global::System.Math.Clamp(amount, 0, 1);
        return Color.FromRgba(
            from.Red + (to.Red - from.Red) * clamped,
            from.Green + (to.Green - from.Green) * clamped,
            from.Blue + (to.Blue - from.Blue) * clamped,
            from.Alpha + (to.Alpha - from.Alpha) * clamped);
    }

    static void DrawLabel(ICanvas canvas, string? label, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        canvas.FontColor = Colors.DarkSlateGray;
        canvas.FontSize = 11;
        canvas.DrawString(
            label,
            (float)x + 6,
            (float)y - 10,
            220,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    void DrawMarkers(ICanvas canvas)
    {
        if (Markers is not { Count: > 0 })
            return;

        foreach (var marker in Markers)
        {
            var screen = WebMercatorTiles.GeoToPixel(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                marker.Position);
            var radius = double.IsFinite(marker.RadiusPixels)
                ? global::System.Math.Max(2, marker.RadiusPixels)
                : 6;
            var selected = IsSelected(MapOverlayKind.Marker, marker.Id);
            canvas.FillColor = marker.Ink
                ?? (selected ? Colors.DarkOrange : Colors.DarkCyan);
            canvas.FillCircle((float)screen.X, (float)screen.Y, (float)radius);
            if (selected)
            {
                canvas.StrokeColor = Colors.Gold;
                canvas.StrokeSize = 2;
                canvas.DrawCircle((float)screen.X, (float)screen.Y, (float)(radius + 3));
            }

            if (string.IsNullOrWhiteSpace(marker.Label))
                continue;

            canvas.FontColor = Colors.DarkSlateGray;
            canvas.FontSize = 11;
            canvas.DrawString(
                marker.Label,
                (float)(screen.X + radius + 4),
                (float)(screen.Y - 10),
                220,
                20,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }
    }

    void DrawSelectedCoordinate(ICanvas canvas)
    {
        if (SelectedCoordinate is not { } selected || SelectedOverlay is not null)
        {
            return;
        }

        var screen = WebMercatorTiles.GeoToPixel(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            selected);
        canvas.StrokeColor = Colors.DarkOrange;
        canvas.StrokeSize = 2;
        canvas.DrawCircle((float)screen.X, (float)screen.Y, 8);
        canvas.DrawLine(
            (float)(screen.X - 11),
            (float)screen.Y,
            (float)(screen.X + 11),
            (float)screen.Y);
        canvas.DrawLine(
            (float)screen.X,
            (float)(screen.Y - 11),
            (float)screen.X,
            (float)(screen.Y + 11));
    }

    void DrawStatus(ICanvas canvas)
    {
        if (!IsLoading
            && string.IsNullOrWhiteSpace(ErrorMessage)
            && !HasStaleTiles)
            return;

        var text = IsLoading
            ? "Loading map…"
            : ErrorMessage ?? "Using cached map tiles";
        var width = (float)global::System.Math.Min(
            global::System.Math.Max(220, Width - 20),
            420);
        canvas.FillColor = Colors.White.WithAlpha(0.86f);
        canvas.FillRectangle(10, 10, width, 34);
        canvas.FontColor = Colors.DarkSlateGray;
        canvas.FontSize = 12;
        canvas.DrawString(
            text,
            18,
            15,
            width - 16,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    void DrawDrawingStatus(ICanvas canvas)
    {
        if (!InteractionOptions.ShowMeasurementResults
            || _drawingKind is not { } kind)
        {
            return;
        }

        var text = GeoMeasurementText.ForDrawing(kind, _drawingPoints);
        var width = (float)global::System.Math.Min(
            global::System.Math.Max(220, Width - 20),
            460);
        var y = (float)global::System.Math.Max(
            48,
            Height - (string.IsNullOrWhiteSpace(Attribution) ? 48 : 78));
        canvas.FillColor = Colors.White.WithAlpha(0.9f);
        canvas.FillRectangle(10, y, width, 34);
        canvas.FontColor = Colors.DarkSlateGray;
        canvas.FontSize = 12;
        canvas.DrawString(
            text,
            18,
            y + 7,
            width - 16,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    void DrawAttribution(ICanvas canvas)
    {
        var attribution = string.IsNullOrWhiteSpace(Attribution)
            ? TileSource?.Template.Attribution
            : Attribution;
        if (string.IsNullOrWhiteSpace(attribution))
            return;

        var width = (float)global::System.Math.Min(
            global::System.Math.Max(160, Width),
            480);
        canvas.FillColor = Colors.White.WithAlpha(0.86f);
        canvas.FillRectangle(0, (float)(Height - 30), width, 30);
        canvas.FontColor = Colors.DarkSlateGray;
        canvas.FontSize = 10;
        canvas.DrawString(
            attribution,
            5,
            (float)(Height - 27),
            width - 10,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    void OnPanUpdated(object? sender, PanUpdatedEventArgs args)
    {
        if (IsDrawing)
            return;

        switch (args.StatusType)
        {
            case GestureStatus.Started:
                BeginCameraInteraction();
                _panStartViewport = Viewport;
                _panLastX = 0;
                _panLastY = 0;
                _panLastAt = DateTimeOffset.UtcNow;
                _panVelocityX = 0;
                _panVelocityY = 0;
                _panActive = true;
                break;
            case GestureStatus.Running:
                if (!_panActive || !HasValidSize())
                    return;

                var now = DateTimeOffset.UtcNow;
                var elapsed = (now - _panLastAt).TotalSeconds;
                var deltaX = args.TotalX - _panLastX;
                var deltaY = args.TotalY - _panLastY;
                if (elapsed > 0.001)
                {
                    _panVelocityX = deltaX / elapsed;
                    _panVelocityY = deltaY / elapsed;
                }

                var center = WebMercatorTiles.PixelToGeo(
                    _panStartViewport.Center,
                    _panStartViewport.Zoom,
                    Width,
                    Height,
                    Width / 2 - args.TotalX,
                    Height / 2 - args.TotalY);
                Viewport = new MapViewport(center, _panStartViewport.Zoom);
                _panLastX = args.TotalX;
                _panLastY = args.TotalY;
                _panLastAt = now;
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_panActive)
                {
                    _panActive = false;
                    EndCameraInteraction();
                }
                if (global::System.Math.Sqrt(
                        _panVelocityX * _panVelocityX
                        + _panVelocityY * _panVelocityY) >= 140)
                {
                    _ = RunInertiaAsync(_panVelocityX, _panVelocityY);
                }
                break;
        }
    }

    void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs args)
    {
        switch (args.Status)
        {
            case GestureStatus.Started:
                if (!HasValidSize())
                    return;

                BeginCameraInteraction();
                _pinchActive = true;
                _pinchStartViewport = Viewport;
                _pinchScreenX = args.ScaleOrigin.X * Width;
                _pinchScreenY = args.ScaleOrigin.Y * Height;
                _pinchAnchor = WebMercatorTiles.PixelToGeo(
                    Viewport.Center,
                    Viewport.Zoom,
                    Width,
                    Height,
                    _pinchScreenX,
                    _pinchScreenY);
                break;
            case GestureStatus.Running:
                if (!HasValidSize())
                    return;

                var scale = global::System.Math.Max(0.01, args.Scale);
                var zoom = global::System.Math.Clamp(
                    _pinchStartViewport.Zoom
                        + global::System.Math.Log(scale, 2),
                    MapViewport.MinimumZoom,
                    MapViewport.MaximumZoom);
                var center = WebMercatorTiles.CenterForAnchor(
                    zoom,
                    Width,
                    Height,
                    _pinchAnchor,
                    _pinchScreenX,
                    _pinchScreenY);
                Viewport = new MapViewport(center, zoom);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_pinchActive)
                {
                    _pinchActive = false;
                    EndCameraInteraction();
                }
                break;
        }
    }

    void OnTapped(object? sender, TappedEventArgs args)
    {
        var position = args.GetPosition(this);
        if (position is null)
            return;

        HandleScreenTap(position.Value.X, position.Value.Y);
    }

    void OnDoubleTapped(object? sender, TappedEventArgs args)
    {
        var position = args.GetPosition(this);
        if (position is null || !HasValidSize() || IsDrawing)
            return;

        var point = position.Value;
        ZoomAt(point.X, point.Y, Viewport.Zoom + 1);
    }

    OverlayHit? HitTestOverlay(double x, double y)
    {
        var marker = HitTestMarker(x, y);
        if (marker is not null)
            return new OverlayHit(MarkerKey(marker), marker.Position, marker);

        var track = HitTestTrack(x, y);
        if (track is not null)
            return new OverlayHit(
                new MapOverlayKey(MapOverlayKind.Track, track.Id),
                track.Points[track.Points.Count / 2],
                null);

        var polygon = HitTestPolygon(x, y);
        if (polygon is not null)
            return new OverlayHit(
                new MapOverlayKey(MapOverlayKind.Polygon, polygon.Id),
                polygon.Points[0],
                null);

        var circle = HitTestCircle(x, y);
        return circle is null
            ? null
            : new OverlayHit(
                new MapOverlayKey(MapOverlayKind.Circle, circle.Id),
                circle.Circle.Center,
                null);
    }

    MapMarker? HitTestMarker(double x, double y)
    {
        if (Markers is not { Count: > 0 })
            return null;

        for (var index = Markers.Count - 1; index >= 0; index--)
        {
            var marker = Markers[index];
            var screen = WebMercatorTiles.GeoToPixel(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                marker.Position);
            var radius = double.IsFinite(marker.RadiusPixels)
                ? global::System.Math.Max(2, marker.RadiusPixels)
                : 6;
            var hitRadius = radius + 8;
            var deltaX = screen.X - x;
            var deltaY = screen.Y - y;
            if (deltaX * deltaX + deltaY * deltaY <= hitRadius * hitRadius)
                return marker;
        }

        return null;
    }

    MapTrackOverlay? HitTestTrack(double x, double y)
    {
        if (Tracks is not { Count: > 0 })
            return null;

        for (var itemIndex = Tracks.Count - 1; itemIndex >= 0; itemIndex--)
        {
            var track = Tracks[itemIndex];
            if (track.Points.Count < 2)
                continue;

            var pixels = WebMercatorTiles.GeoPathToPixels(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                track.Points);
            for (var index = 1; index < pixels.Count; index++)
            {
                if (DistanceToSegmentSquared(
                        x,
                        y,
                        pixels[index - 1].X,
                        pixels[index - 1].Y,
                        pixels[index].X,
                        pixels[index].Y) <= 100)
                {
                    return track;
                }
            }
        }

        return null;
    }

    MapPolygonOverlay? HitTestPolygon(double x, double y)
    {
        if (Polygons is not { Count: > 0 })
            return null;

        for (var itemIndex = Polygons.Count - 1; itemIndex >= 0; itemIndex--)
        {
            var polygon = Polygons[itemIndex];
            if (polygon.Points.Count < 3)
                continue;

            var pixels = WebMercatorTiles.GeoPathToPixels(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                polygon.Points);
            if (ContainsPoint(pixels, x, y))
                return polygon;
        }

        return null;
    }

    MapCircleOverlay? HitTestCircle(double x, double y)
    {
        if (Circles is not { Count: > 0 })
            return null;

        for (var index = Circles.Count - 1; index >= 0; index--)
        {
            var circle = Circles[index];
            var center = WebMercatorTiles.GeoToPixel(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                circle.Circle.Center);
            var radius = CircleRadiusPixels(circle.Circle);
            var deltaX = center.X - x;
            var deltaY = center.Y - y;
            if (deltaX * deltaX + deltaY * deltaY <= (radius + 8) * (radius + 8))
                return circle;
        }

        return null;
    }

    double CircleRadiusPixels(GeoCircle circle)
    {
        var worldPixels = WebMercatorTiles.TileSizePixels
            * global::System.Math.Pow(2, Viewport.Zoom);
        var latitudeRadians = circle.Center.Latitude
            * global::System.Math.PI
            / 180d;
        var metersPerPixel = 2
            * global::System.Math.PI
            * GeoDistance.MeanEarthRadiusMeters
            * global::System.Math.Max(0.01, global::System.Math.Cos(latitudeRadians))
            / worldPixels;
        return circle.RadiusMeters / metersPerPixel;
    }

    static bool ContainsPoint(
        IReadOnlyList<(double X, double Y)> polygon,
        double x,
        double y)
    {
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var previous = (index + polygon.Count - 1) % polygon.Count;
            var currentPoint = polygon[index];
            var previousPoint = polygon[previous];
            if ((currentPoint.Y > y) == (previousPoint.Y > y))
                continue;

            var crossingX = (previousPoint.X - currentPoint.X)
                * (y - currentPoint.Y)
                / (previousPoint.Y - currentPoint.Y)
                + currentPoint.X;
            if (x < crossingX)
                inside = !inside;
        }

        return inside;
    }

    static double DistanceToSegmentSquared(
        double x,
        double y,
        double startX,
        double startY,
        double endX,
        double endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var lengthSquared = deltaX * deltaX + deltaY * deltaY;
        if (lengthSquared <= double.Epsilon)
        {
            var pointDeltaX = x - startX;
            var pointDeltaY = y - startY;
            return pointDeltaX * pointDeltaX + pointDeltaY * pointDeltaY;
        }

        var fraction = global::System.Math.Clamp(
            ((x - startX) * deltaX + (y - startY) * deltaY) / lengthSquared,
            0,
            1);
        var closestX = startX + fraction * deltaX;
        var closestY = startY + fraction * deltaY;
        var closestDeltaX = x - closestX;
        var closestDeltaY = y - closestY;
        return closestDeltaX * closestDeltaX + closestDeltaY * closestDeltaY;
    }

    async Task<bool> CopySelectionAsync(
        string? text,
        CancellationToken cancellationToken)
    {
        if (text is null)
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (ClipboardWriter is not null)
            {
                await ClipboardWriter(text, cancellationToken);
                return true;
            }

            await Clipboard.Default.SetTextAsync(text);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    void ApplyInteractionSelection(
        GeoCoordinate coordinate,
        MapMarker? marker,
        MapOverlayKey? overlay)
    {
        var previousOverlay = SelectedOverlay;
        SetSelection(coordinate, marker, overlay);

        if (marker is not null)
            MarkerSelected?.Invoke(marker);
        PointSelected?.Invoke(coordinate);
        if (overlay is { } selected
            && previousOverlay != selected)
        {
            OverlaySelected?.Invoke(selected);
        }
    }

    bool SetSelection(
        GeoCoordinate? coordinate,
        MapMarker? marker,
        MapOverlayKey? overlay)
    {
        var selectionChanged = _selectedCoordinate != coordinate
            || SelectedOverlay != overlay;
        var overlayChanged = SelectedOverlay != overlay;
        _selectedCoordinate = coordinate;
        SelectedMarker = marker;
        SelectedOverlay = overlay;
        InvalidateMap();
        if (overlayChanged)
            OverlaySelectionChanged?.Invoke(overlay);
        if (selectionChanged)
            SelectionChanged?.Invoke();
        return selectionChanged;
    }

    void ReconcileOverlaySelection()
    {
        if (SelectedOverlay is { } key)
        {
            if (TryResolveOverlay(key, out var coordinate, out var resolvedMarker))
                SetSelection(coordinate, resolvedMarker, key);
            else
                ClearSelection();
            return;
        }

        var marker = _selectedCoordinate is { } selected
            ? Markers?.FirstOrDefault(item => item.Position == selected)
            : null;
        SetSelection(
            _selectedCoordinate,
            marker,
            marker is null ? null : MarkerKey(marker));
    }

    bool TryResolveOverlay(
        MapOverlayKey key,
        out GeoCoordinate coordinate,
        out MapMarker? marker)
    {
        marker = null;
        switch (key.Kind)
        {
            case MapOverlayKind.Marker:
                marker = Markers?.FirstOrDefault(item => item.Id == key.Id);
                if (marker is not null)
                {
                    coordinate = marker.Position;
                    return true;
                }
                break;
            case MapOverlayKind.Circle:
                var circle = Circles?.FirstOrDefault(item => item.Id == key.Id);
                if (circle is not null)
                {
                    coordinate = circle.Circle.Center;
                    return true;
                }
                break;
            case MapOverlayKind.Track:
                var track = Tracks?.FirstOrDefault(item => item.Id == key.Id);
                if (track is { Points.Count: > 0 })
                {
                    coordinate = track.Points[track.Points.Count / 2];
                    return true;
                }
                break;
            case MapOverlayKind.Polygon:
                var polygon = Polygons?.FirstOrDefault(item => item.Id == key.Id);
                if (polygon is { Points.Count: > 0 })
                {
                    coordinate = polygon.Points[0];
                    return true;
                }
                break;
        }

        coordinate = default;
        return false;
    }

    static MapOverlayKey MarkerKey(MapMarker marker) =>
        new(MapOverlayKind.Marker, marker.Id);

    bool IsSelected(MapOverlayKind kind, string id) =>
        SelectedOverlay == new MapOverlayKey(kind, id);

    static bool HasEnoughDrawingPoints(GeoDrawingKind kind, int count) =>
        kind switch
        {
            GeoDrawingKind.Point => count >= 1,
            GeoDrawingKind.Circle => count >= 2,
            GeoDrawingKind.Polyline => count >= 2,
            GeoDrawingKind.Polygon => count >= 3,
            GeoDrawingKind.Rectangle => count >= 2,
            _ => false,
        };

    readonly record struct OverlayHit(
        MapOverlayKey Key,
        GeoCoordinate Coordinate,
        MapMarker? Marker);

    async Task RunInertiaAsync(double velocityX, double velocityY)
    {
        CancelInertia();
        BeginCameraInteraction();
        using var cancellation = new CancellationTokenSource();
        _inertiaCancellation = cancellation;
        try
        {
            while (global::System.Math.Sqrt(
                       velocityX * velocityX + velocityY * velocityY) >= 24)
            {
                await Task.Delay(16, cancellation.Token);
                if (!HasValidSize())
                    break;

                var deltaX = velocityX * 0.016;
                var deltaY = velocityY * 0.016;
                var center = WebMercatorTiles.PixelToGeo(
                    Viewport.Center,
                    Viewport.Zoom,
                    Width,
                    Height,
                    Width / 2 - deltaX,
                    Height / 2 - deltaY);
                Viewport = new MapViewport(center, Viewport.Zoom);
                velocityX *= 0.9;
                velocityY *= 0.9;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_inertiaCancellation, cancellation))
                _inertiaCancellation = null;
            EndCameraInteraction();
        }
    }

    void QueueTileRefresh(bool immediate = false)
    {
        if (TileSource is null
            || !TileLoadingEnabled
            || !HasValidSize()
            || Handler is null)
            return;

        Interlocked.Increment(ref _refreshRevision);
        if (immediate)
            _refreshImmediateRequested = true;

        if (_refreshScheduleTask is not null)
            return;

        var schedule = new CancellationTokenSource();
        _refreshScheduleCancellation = schedule;
        var task = ScheduleTileRefreshAsync(schedule);
        if (ReferenceEquals(_refreshScheduleCancellation, schedule))
            _refreshScheduleTask = task;
    }

    async Task ScheduleTileRefreshAsync(CancellationTokenSource schedule)
    {
        try
        {
            while (!schedule.IsCancellationRequested)
            {
                var immediate = _refreshImmediateRequested;
                _refreshImmediateRequested = false;
                if (!immediate)
                {
                    var quietRevision = Volatile.Read(ref _refreshRevision);
                    while (true)
                    {
                        await Task.Delay(TileRefreshDebounce, schedule.Token);
                        if (_refreshImmediateRequested)
                            break;

                        var revision = Volatile.Read(ref _refreshRevision);
                        if (revision == quietRevision)
                            break;

                        quietRevision = revision;
                    }
                }

                if (_interactionDepth > 0)
                    continue;

                var refreshRevision = Volatile.Read(ref _refreshRevision);
                await RefreshTilesAsync(schedule.Token);
                if (schedule.IsCancellationRequested)
                    return;

                if (refreshRevision == Volatile.Read(ref _refreshRevision)
                    && !_refreshImmediateRequested)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_refreshScheduleCancellation, schedule))
            {
                _refreshScheduleCancellation = null;
                _refreshScheduleTask = null;
                _refreshImmediateRequested = false;
            }
            schedule.Dispose();
        }
    }

    void StopRefreshSchedule()
    {
        var schedule = _refreshScheduleCancellation;
        _refreshScheduleCancellation = null;
        _refreshScheduleTask = null;
        _refreshImmediateRequested = false;
        schedule?.Cancel();
    }

    /// <summary>Loads the exact tile set visible in the current viewport.</summary>
    public async Task RefreshTilesAsync(CancellationToken cancellationToken = default)
    {
        var source = TileSource;
        if (source is null || !TileLoadingEnabled || !HasValidSize())
            return;

        var generation = Interlocked.Increment(ref _tileRefreshGeneration);
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(
            ref _tileRefreshCancellation,
            cancellation);
        previous?.Cancel();
        var token = cancellation.Token;
        IsLoading = true;
        ErrorMessage = null;
        InvalidateMap();

        try
        {
            var keys = GetVisibleTileKeys();
            var candidates = keys
                .Where(key => !_tiles.ContainsKey(key) || _staleTiles.Contains(key))
                .OrderBy(key => TileDistanceFromViewportCenter(key))
                .ToArray();
            var failed = 0;
            var acceptResults = true;
            var pending = candidates
                .Select(key => LoadDecodedTileAsync(source, key, token))
                .ToList();
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);
                pending.Remove(completed);
                var result = await completed;

                if (token.IsCancellationRequested
                    || !IsCurrentTileRefresh(source, generation))
                {
                    acceptResults = false;
                    DisposeDecodedImage(result.Image);
                    continue;
                }

                if (result.Image is null)
                {
                    if (result.Failed)
                        failed++;
                    continue;
                }

                ReplaceTile(result.Key, result.Image, result.IsStale);
                TrimTiles(keys);
                InvalidateMap();
            }

            if (!acceptResults)
                return;

            TrimTiles(keys);
            if (failed > 0)
            {
                var available = keys.Count(key => _tiles.ContainsKey(key));
                ErrorMessage = available > 0
                    ? $"{failed} map tile{(failed == 1 ? string.Empty : "s")} unavailable. Retry."
                    : "Map tiles are unavailable. Check the connection and retry.";
            }
            InvalidateMap();
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested
            || !IsCurrentTileRefresh(source, generation))
        {
            PerformanceCounters.RecordRefreshCancellation();
        }
        catch (Exception exception) when (IsCurrentTileRefresh(source, generation))
        {
            ErrorMessage = $"Map tiles are unavailable: {exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_tileRefreshCancellation, cancellation))
            {
                _tileRefreshCancellation = null;
                IsLoading = false;
                InvalidateMap();
            }
        }
    }

    async Task<DecodedTile> LoadDecodedTileAsync(
        IMapRasterSource source,
        MapTileKey key,
        CancellationToken cancellationToken)
    {
        await _tileRequestGate.WaitAsync(cancellationToken);
        PerformanceCounters.RecordTileRequestStarted(key);
        try
        {
            var tile = await source.GetTileAsync(key, cancellationToken);
            if (tile is null)
                return new DecodedTile(key, null, false, true);

            var image = TileDecoder?.Invoke(tile) ?? Decode(tile);
            if (image is not null)
                PerformanceCounters.RecordDecodedTileCreated();
            return new DecodedTile(
                key,
                image,
                tile.IsStale,
                image is null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new DecodedTile(key, null, false, false);
        }
        catch
        {
            return new DecodedTile(key, null, false, true);
        }
        finally
        {
            PerformanceCounters.RecordTileRequestCompleted(key);
            _tileRequestGate.Release();
        }
    }

    double TileDistanceFromViewportCenter(MapTileKey key)
    {
        var rectangle = WebMercatorTiles.TilePixelRect(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            key);
        var deltaX = rectangle.X + rectangle.Width / 2 - Width / 2;
        var deltaY = rectangle.Y + rectangle.Height / 2 - Height / 2;
        return deltaX * deltaX + deltaY * deltaY;
    }

    readonly record struct DecodedTile(
        MapTileKey Key,
        GraphicsImage? Image,
        bool IsStale,
        bool Failed);

    bool IsCurrentTileRefresh(IMapRasterSource source, long generation) =>
        ReferenceEquals(source, TileSource)
        && Volatile.Read(ref _tileRefreshGeneration) == generation;

    void DisposeDecodedImage(GraphicsImage? image, bool stored = false)
    {
        if (image is IDisposable disposable)
        {
            disposable.Dispose();
            PerformanceCounters.RecordDecodedTileDisposed();
            if (stored)
                PerformanceCounters.RecordDecodedTileRemoved();
        }
    }

    static GraphicsImage? Decode(MapRasterTile tile)
    {
        try
        {
            using var stream = new MemoryStream(tile.PngBytes, writable: false);
            return PlatformImage.FromStream(stream);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    void ReplaceTile(MapTileKey key, GraphicsImage image, bool isStale)
    {
        if (_tiles.Remove(key, out var previous)
            )
            DisposeDecodedImage(previous, stored: true);

        _tiles[key] = image;
        _tileLastUsed[key] = ++_tileUseCounter;
        PerformanceCounters.RecordDecodedTileStored();
        if (isStale)
            _staleTiles.Add(key);
        else
            _staleTiles.Remove(key);
    }

    void TrimTiles(IReadOnlyCollection<MapTileKey> visibleKeys)
    {
        var visible = visibleKeys.ToHashSet();
        while (_tiles.Count > MaximumCachedTiles)
        {
            var victim = _tileLastUsed
                .Where(item => !visible.Contains(item.Key))
                .OrderBy(item => item.Value)
                .Select(item => item.Key)
                .FirstOrDefault();
            if (!_tiles.ContainsKey(victim))
            {
                victim = _tileLastUsed
                    .OrderBy(item => item.Value)
                    .Select(item => item.Key)
                    .FirstOrDefault();
            }

            if (!_tiles.ContainsKey(victim))
                break;

            RemoveTile(victim);
            PerformanceCounters.RecordCacheEviction();
        }
    }

    void RemoveTile(MapTileKey key)
    {
        if (_tiles.Remove(key, out var tile))
            DisposeDecodedImage(tile, stored: true);

        _tileLastUsed.Remove(key);
        _staleTiles.Remove(key);
    }

    void DisposeTiles()
    {
        foreach (var tile in _tiles.Values)
            DisposeDecodedImage(tile, stored: true);

        _tiles.Clear();
        _tileLastUsed.Clear();
        _staleTiles.Clear();
    }

    void CancelInertia()
    {
        _inertiaCancellation?.Cancel();
        _inertiaCancellation = null;
    }

    /// <summary>Requests a redraw and records it for diagnostics.</summary>
    public void RequestRender()
    {
        PerformanceCounters.RecordRedrawRequest();
        Invalidate();
    }

    void InvalidateMap()
    {
        PerformanceCounters.RecordRedrawRequest();
        Invalidate();
    }

    bool HasValidSize() =>
        double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}
