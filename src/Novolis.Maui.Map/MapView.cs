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
    readonly Dictionary<MapTileKey, GraphicsImage> _tiles = new();
    CancellationTokenSource? _tileRefreshCancellation;
    CancellationTokenSource? _inertiaCancellation;
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

    /// <summary>Creates a map view with a neutral initial viewport.</summary>
    public MapView()
    {
        Drawable = this;
        BackgroundColor = Colors.LightGray;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

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

    /// <summary>Camera state.</summary>
    public MapViewport Viewport
    {
        get;
        set
        {
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
            ClearTiles();
            QueueTileRefresh();
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

    /// <summary>Recoverable provider error displayed over the map.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Raised when the user selects a geographic point.</summary>
    public event Action<GeoCoordinate>? PointSelected;

    /// <summary>Raised when the user selects a marker.</summary>
    public event Action<MapMarker>? MarkerSelected;

    /// <summary>Sets the camera center and zoom.</summary>
    public void SetViewport(GeoCoordinate center, double zoom) =>
        Viewport = new MapViewport(center, zoom);

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.LightGray;
        canvas.FillRectangle(dirtyRect);
        if (Width <= 0 || Height <= 0)
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
        foreach (var key in WebMercatorTiles.VisibleTiles(
                     Viewport.Center,
                     Viewport.Zoom,
                     Width,
                     Height))
        {
            var rectangle = WebMercatorTiles.TilePixelRect(
                Viewport.Center,
                Viewport.Zoom,
                Width,
                Height,
                key);
            if (!_tiles.TryGetValue(key, out var image))
                continue;

            canvas.DrawImage(
                image,
                (float)rectangle.X,
                (float)rectangle.Y,
                (float)rectangle.Width,
                (float)rectangle.Height);
        }
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
        if (!IsLoading && string.IsNullOrWhiteSpace(ErrorMessage))
            return;

        var text = IsLoading ? "Loading map…" : ErrorMessage!;
        canvas.FillColor = Colors.White.WithAlpha(0.86f);
        canvas.FillRectangle(10, 10, 220, 30);
        canvas.FontColor = Colors.DarkSlateGray;
        canvas.FontSize = 12;
        canvas.DrawString(
            text,
            18,
            15,
            204,
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

        canvas.FillColor = Colors.White.WithAlpha(0.86f);
        canvas.FillRectangle(0, (float)(Height - 26), 260, 26);
        canvas.FontColor = Colors.DarkSlateGray;
        canvas.FontSize = 10;
        canvas.DrawString(
            attribution,
            5,
            (float)(Height - 23),
            250,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    void OnPanUpdated(object? sender, PanUpdatedEventArgs args)
    {
        switch (args.StatusType)
        {
            case GestureStatus.Started:
                CancelInertia();
                _panStartViewport = Viewport;
                _panLastX = 0;
                _panLastY = 0;
                _panLastAt = DateTimeOffset.UtcNow;
                _panVelocityX = 0;
                _panVelocityY = 0;
                _panActive = true;
                break;
            case GestureStatus.Running:
                if (!_panActive || Width <= 0 || Height <= 0)
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
                _panActive = false;
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
                CancelInertia();
                if (Width <= 0 || Height <= 0)
                    return;

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
                if (Width <= 0 || Height <= 0)
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
        }
    }

    void OnTapped(object? sender, TappedEventArgs args)
    {
        var position = args.GetPosition(this);
        if (position is null || Width <= 0 || Height <= 0)
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
        using var cancellation = new CancellationTokenSource();
        _inertiaCancellation = cancellation;
        try
        {
            while (global::System.Math.Sqrt(
                       velocityX * velocityX + velocityY * velocityY) >= 24)
            {
                await Task.Delay(16, cancellation.Token);
                if (Width <= 0 || Height <= 0)
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
        }
    }

    void QueueTileRefresh()
    {
        if (TileSource is null || Width <= 0 || Height <= 0)
            return;

        _ = RefreshTilesAsync();
    }

    async Task RefreshTilesAsync()
    {
        var source = TileSource;
        if (source is null || Width <= 0 || Height <= 0)
            return;

        _tileRefreshCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _tileRefreshCancellation = cancellation;
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
            var loaded = await Task.WhenAll(keys.Select(async key =>
            {
                var tile = await source.GetTileAsync(key, token);
                return (key, image: tile is null ? null : Decode(tile));
            }));

            if (token.IsCancellationRequested || !ReferenceEquals(source, TileSource))
                return;

            foreach (var result in loaded)
            {
                if (result.image is not null)
                    ReplaceTile(result.key, result.image);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                ErrorMessage = exception.Message;
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

    static GraphicsImage? Decode(MapRasterTile tile)
    {
        try
        {
            using var stream = new MemoryStream(tile.PngBytes, writable: false);
            return PlatformImage.FromStream(stream);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    void ReplaceTile(MapTileKey key, GraphicsImage image)
    {
        if (_tiles.Remove(key, out var previous)
            && previous is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _tiles[key] = image;
    }

    void ClearTiles()
    {
        foreach (var tile in _tiles.Values)
        {
            if (tile is IDisposable disposable)
                disposable.Dispose();
        }

        _tiles.Clear();
    }

    void CancelInertia()
    {
        _inertiaCancellation?.Cancel();
        _inertiaCancellation = null;
    }
}
