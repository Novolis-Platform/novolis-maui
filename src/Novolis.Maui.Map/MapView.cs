using Microsoft.Maui.Controls;
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
    static readonly TimeSpan TileRefreshDebounce = TimeSpan.FromMilliseconds(90);

    readonly Dictionary<MapTileKey, GraphicsImage> _tiles = new();
    readonly Dictionary<MapTileKey, long> _tileLastUsed = new();
    readonly HashSet<MapTileKey> _staleTiles = new();
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
            Invalidate();
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
            Invalidate();
        }
    }

    /// <summary>Markers rendered over the map.</summary>
    public IReadOnlyList<MapMarker>? Markers
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>Circles rendered over the map.</summary>
    public IReadOnlyList<MapCircleOverlay>? Circles
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>Connected geographic tracks rendered below markers.</summary>
    public IReadOnlyList<MapTrackOverlay>? Tracks
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>Selected geographic coordinate, if any.</summary>
    public GeoCoordinate? SelectedCoordinate
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>Optional attribution override shown in the lower-left corner.</summary>
    public string? Attribution
    {
        get;
        set
        {
            field = value;
            Invalidate();
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
                Invalidate();
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
        && WebMercatorTiles.VisibleTiles(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height).Any(_staleTiles.Contains);

    /// <summary>Raised when the user selects a geographic point.</summary>
    public event Action<GeoCoordinate>? PointSelected;

    /// <summary>Raised when the user selects a marker.</summary>
    public event Action<MapMarker>? MarkerSelected;

    /// <summary>Sets the camera center and zoom.</summary>
    public void SetViewport(GeoCoordinate center, double zoom) =>
        Viewport = new MapViewport(center, zoom);

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
        Invalidate();
        QueueTileRefresh(immediate: true);
    }

    /// <summary>Releases decoded tile images currently held by this view.</summary>
    public void ClearTiles()
    {
        Interlocked.Increment(ref _tileRefreshGeneration);
        _tileRefreshCancellation?.Cancel();
        ErrorMessage = null;
        DisposeTiles();
        Invalidate();
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
        DrawTracks(canvas);
        DrawMarkers(canvas);
        DrawSelectedCoordinate(canvas);
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
        var visibleKeys = WebMercatorTiles.VisibleTiles(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height);
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

            canvas.FillColor = Colors.CornflowerBlue.WithAlpha(0.18f);
            canvas.FillCircle((float)center.X, (float)center.Y, (float)radius);
            canvas.StrokeColor = Colors.DarkSlateBlue;
            canvas.StrokeSize = 2;
            canvas.DrawCircle((float)center.X, (float)center.Y, (float)radius);
        }
    }

    void DrawTracks(ICanvas canvas)
    {
        if (Tracks is not { Count: > 0 })
            return;

        canvas.StrokeColor = Colors.DarkSlateBlue;
        canvas.StrokeSize = 3;
        foreach (var track in Tracks)
        {
            for (var index = 1; index < track.Points.Count; index++)
            {
                var first = WebMercatorTiles.GeoToPixel(
                    Viewport.Center,
                    Viewport.Zoom,
                    Width,
                    Height,
                    track.Points[index - 1]);
                var second = WebMercatorTiles.GeoToPixel(
                    Viewport.Center,
                    Viewport.Zoom,
                    Width,
                    Height,
                    track.Points[index]);
                canvas.DrawLine(
                    (float)first.X,
                    (float)first.Y,
                    (float)second.X,
                    (float)second.Y);
            }
        }
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
            var selected = SelectedCoordinate == marker.Position;
            canvas.FillColor = selected ? Colors.DarkOrange : Colors.DarkCyan;
            canvas.FillCircle((float)screen.X, (float)screen.Y, (float)radius);

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
        if (SelectedCoordinate is not { } selected
            || Markers?.Any(marker => marker.Position == selected) == true)
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
        if (position is null || !HasValidSize())
            return;

        var point = position.Value;
        var marker = HitTestMarker(point.X, point.Y);
        if (marker is not null)
        {
            SelectedCoordinate = marker.Position;
            MarkerSelected?.Invoke(marker);
            PointSelected?.Invoke(marker.Position);
            return;
        }

        var selected = WebMercatorTiles.PixelToGeo(
            Viewport.Center,
            Viewport.Zoom,
            Width,
            Height,
            point.X,
            point.Y);
        SelectedCoordinate = selected;
        PointSelected?.Invoke(selected);
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

    async Task RefreshTilesAsync(CancellationToken cancellationToken = default)
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
        Invalidate();

        try
        {
            var keys = WebMercatorTiles.VisibleTiles(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height);
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
                    if (result.Image is IDisposable disposable)
                        disposable.Dispose();
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
                Invalidate();
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
            Invalidate();
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested
            || !IsCurrentTileRefresh(source, generation))
        {
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
                Invalidate();
            }
        }
    }

    async Task<DecodedTile> LoadDecodedTileAsync(
        IMapRasterSource source,
        MapTileKey key,
        CancellationToken cancellationToken)
    {
        try
        {
            var tile = await source.GetTileAsync(key, cancellationToken);
            if (tile is null)
                return new DecodedTile(key, null, false, true);

            var image = Decode(tile);
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

    static void DisposeDecodedTiles(IEnumerable<DecodedTile> tiles)
    {
        foreach (var tile in tiles)
        {
            if (tile.Image is IDisposable disposable)
                disposable.Dispose();
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
            && previous is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _tiles[key] = image;
        _tileLastUsed[key] = ++_tileUseCounter;
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
        }
    }

    void RemoveTile(MapTileKey key)
    {
        if (_tiles.Remove(key, out var tile)
            && tile is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _tileLastUsed.Remove(key);
        _staleTiles.Remove(key);
    }

    void DisposeTiles()
    {
        foreach (var tile in _tiles.Values)
        {
            if (tile is IDisposable disposable)
                disposable.Dispose();
        }

        _tiles.Clear();
        _tileLastUsed.Clear();
        _staleTiles.Clear();
    }

    void CancelInertia()
    {
        _inertiaCancellation?.Cancel();
        _inertiaCancellation = null;
    }

    bool HasValidSize() =>
        double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}
