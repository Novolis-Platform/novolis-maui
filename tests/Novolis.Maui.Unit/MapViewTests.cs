using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;
using Novolis.Maui.Map;

namespace Novolis.Maui.Unit;

public sealed class MapViewTests
{
    [Test]
    public async Task MapViewport_validates_zoom_and_preserves_center()
    {
        var center = new GeoCoordinate(58.14623, 7.99517);
        var viewport = new MapViewport(center, 14);

        await Assert.That(viewport.Center).IsEqualTo(center);
        await Assert.That(viewport.Zoom).IsEqualTo(14d);
        await Assert.That(() => new MapViewport(center, 23))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task MapView_exposes_geographic_overlay_state()
    {
        var first = new GeoCoordinate(58.14, 7.99);
        var second = new GeoCoordinate(58.15, 8.01);
        var map = new MapView
        {
            Markers = [new MapMarker("office", first, "Office")],
            Circles =
            [
                new MapCircleOverlay(
                    "area",
                    new GeoCircle(first, 200)),
            ],
            Tracks = [new MapTrackOverlay("route", [first, second])],
            SelectedCoordinate = second,
        };

        await Assert.That(map.Markers).Count().IsEqualTo(1);
        await Assert.That(map.Circles).Count().IsEqualTo(1);
        await Assert.That(map.Tracks).Count().IsEqualTo(1);
        await Assert.That(map.SelectedCoordinate).IsEqualTo(second);
    }

    [Test]
    public async Task MapView_overlay_models_preserve_labels_and_styling()
    {
        var first = new GeoCoordinate(58.14, 7.99);
        var second = new GeoCoordinate(58.15, 8.01);
        var map = new MapView
        {
            Circles =
            [
                new MapCircleOverlay(
                    "area",
                    new GeoCircle(first, 200),
                    "Area",
                    Colors.Red),
            ],
            Tracks =
            [
                new MapTrackOverlay(
                    "route",
                    [first, second],
                    "Route",
                    Colors.Blue,
                    Colors.Green),
            ],
            Polygons =
            [
                new MapPolygonOverlay(
                    "zone",
                    [first, second, new GeoCoordinate(58.16, 8.02)],
                    "Zone",
                    Colors.Purple,
                    Colors.Yellow),
            ],
        };

        await Assert.That(map.Circles![0].Label).IsEqualTo("Area");
        await Assert.That(map.Circles[0].Ink).IsEqualTo(Colors.Red);
        await Assert.That(map.Tracks![0].Label).IsEqualTo("Route");
        await Assert.That(map.Tracks[0].FromInk).IsEqualTo(Colors.Blue);
        await Assert.That(map.Tracks[0].ToInk).IsEqualTo(Colors.Green);
        await Assert.That(map.Polygons![0].Label).IsEqualTo("Zone");
        await Assert.That(map.Polygons[0].Fill).IsEqualTo(Colors.Yellow);
    }

    [Test]
    public async Task MapView_bounds_concurrent_requests_and_records_visible_work()
    {
        var active = 0;
        var peak = 0;
        var source = new RecordingRasterSource
        {
            Handler = async (_, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref active);
                while (true)
                {
                    var observed = Volatile.Read(ref peak);
                    if (current <= observed
                        || Interlocked.CompareExchange(ref peak, current, observed) == observed)
                    {
                        break;
                    }
                }

                await Task.Delay(5, cancellationToken);
                Interlocked.Decrement(ref active);
                return null;
            },
        };
        var map = new MapView
        {
            TileSource = source,
            Viewport = new MapViewport(new GeoCoordinate(0, 0), 8),
        };
        map.Measure(2_048, 2_048);
        map.Arrange(new Rect(0, 0, 2_048, 2_048));

        await map.RefreshTilesAsync();

        await Assert.That(peak).IsLessThanOrEqualTo(6);
        await Assert.That(map.PerformanceCounters.VisibleTileCalculations).IsGreaterThan(0);
        await Assert.That(map.PerformanceCounters.TileRequestsStarted)
            .IsEqualTo(source.Requests.Count);
        await Assert.That(map.PerformanceCounters.TileRequestsCompleted)
            .IsEqualTo(source.Requests.Count);
        await Assert.That(map.PerformanceCounters.DuplicateTileRequests).IsEqualTo(0);
    }

    [Test]
    public async Task MapView_does_not_load_tiles_when_loading_is_disabled()
    {
        var source = new RecordingRasterSource
        {
            Handler = (_, _) => Task.FromResult<MapRasterTile?>(null),
        };
        var map = new MapView
        {
            TileSource = source,
            TileLoadingEnabled = false,
        };
        map.Measure(512, 512);
        map.Arrange(new Rect(0, 0, 512, 512));

        await map.RefreshTilesAsync();

        await Assert.That(source.Requests).IsEmpty();
        await Assert.That(map.PerformanceCounters.TileRequestsStarted).IsEqualTo(0);
    }

    [Test]
    public async Task MapView_opt_in_drawing_and_copy_return_developer_data()
    {
        var first = new GeoCoordinate(58.14, 7.99);
        var second = new GeoCoordinate(58.15, 8.01);
        var map = new MapView
        {
            InteractionOptions = new MapInteractionOptions
            {
                EnableDrawing = true,
                EnableClipboardShortcuts = true,
            },
        };
        GeoDrawing? completed = null;
        string? copied = null;
        map.DrawingCompleted += drawing => completed = drawing;
        map.ClipboardWriter = (text, _) =>
        {
            copied = text;
            return Task.CompletedTask;
        };

        await Assert.That(map.BeginDrawing(GeoDrawingKind.Polyline)).IsTrue();
        await Assert.That(map.AddDrawingPoint(first)).IsTrue();
        await Assert.That(map.AddDrawingPoint(second)).IsTrue();
        await Assert.That(map.CompleteDrawing()).IsNotNull();
        await Assert.That(completed!.LengthMeters).IsGreaterThan(1_000d);

        map.Markers =
        [
            new MapMarker(
                "office",
                first,
                "Office",
                Metadata: new Dictionary<string, string> { ["kind"] = "poi" }),
        ];
        map.SelectedCoordinate = first;
        await Assert.That(await map.CopySelectedCoordinateAsync()).IsTrue();
        await Assert.That(copied).IsEqualTo("58.140000, 7.990000");
        await Assert.That(await map.CopySelectedJsonAsync()).IsTrue();
        await Assert.That(copied).Contains("\"id\": \"office\"");
    }

    [Test]
    public async Task MapView_keyboard_commands_are_disabled_until_opted_in()
    {
        var map = new MapView
        {
            SelectedCoordinate = new GeoCoordinate(58.14, 7.99),
        };
        map.Measure(512, 512);
        map.Arrange(new Rect(0, 0, 512, 512));
        var initialViewport = map.Viewport;
        var copyAttempts = 0;
        map.ClipboardWriter = (_, _) =>
        {
            copyAttempts++;
            return Task.CompletedTask;
        };

        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.CopyCoordinate))
            .IsFalse();
        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.ZoomIn))
            .IsFalse();
        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.PanLeft))
            .IsFalse();
        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.CompleteDrawing))
            .IsFalse();
        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.CancelDrawing))
            .IsFalse();
        await Assert.That(copyAttempts).IsEqualTo(0);
        await Assert.That(map.Viewport).IsEqualTo(initialViewport);

        map.InteractionOptions = new MapInteractionOptions
        {
            EnableClipboardShortcuts = true,
            EnableKeyboardNavigation = true,
            EnableDrawing = true,
        };
        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.CopyCoordinate))
            .IsTrue();
        await Assert.That(await map.ExecuteKeyboardCommandAsync(MapKeyboardCommand.ZoomIn))
            .IsTrue();
        await Assert.That(map.Viewport.Zoom).IsGreaterThan(initialViewport.Zoom);
    }

    [Test]
    public async Task MapView_copy_returns_false_for_clipboard_failure_and_preserves_cancellation()
    {
        var map = new MapView
        {
            SelectedCoordinate = new GeoCoordinate(58.14, 7.99),
            ClipboardWriter = (_, _) =>
                Task.FromException(new InvalidOperationException("clipboard unavailable")),
        };

        await Assert.That(await map.CopySelectedCoordinateAsync()).IsFalse();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () =>
                await map.CopySelectedCoordinateAsync(cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task MapView_selection_retains_marker_payload_and_does_not_repeat_state_events()
    {
        var point = new GeoCoordinate(58.14, 7.99);
        var marker = new MapMarker(
            "office",
            point,
            "Office",
            Metadata: new Dictionary<string, string> { ["kind"] = "poi" },
            Tag: "payload");
        var map = new MapView { Markers = [marker] };
        var selectionChanges = 0;
        map.SelectionChanged += () => selectionChanges++;

        map.SelectedCoordinate = point;
        map.SelectedCoordinate = point;

        await Assert.That(map.SelectedMarker).IsEqualTo(marker);
        await Assert.That(map.SelectedMarker!.Metadata!["kind"]).IsEqualTo("poi");
        await Assert.That(map.SelectedMarker.Tag).IsEqualTo("payload");
        await Assert.That(selectionChanges).IsEqualTo(1);
    }

    [Test]
    public async Task MapView_circle_drawing_uses_the_latest_edge_preview()
    {
        var center = new GeoCoordinate(58.14, 7.99);
        var firstEdge = new GeoCoordinate(58.15, 8.01);
        var latestEdge = new GeoCoordinate(58.16, 8.03);
        var map = new MapView
        {
            InteractionOptions = new MapInteractionOptions { EnableDrawing = true },
        };

        await Assert.That(map.BeginDrawing(GeoDrawingKind.Circle)).IsTrue();
        await Assert.That(map.AddDrawingPoint(center)).IsTrue();
        await Assert.That(map.AddDrawingPoint(firstEdge)).IsTrue();
        await Assert.That(map.AddDrawingPoint(latestEdge)).IsTrue();

        var drawing = map.CompleteDrawing();
        await Assert.That(drawing).IsNotNull();
        await Assert.That(drawing!.Points).Count().IsEqualTo(2);
        await Assert.That(drawing.Points[1]).IsEqualTo(latestEdge);
    }

    [Test]
    public async Task MapView_registers_a_native_double_tap_zoom_gesture()
    {
        var doubleTap = mapGestureRecognizers(new MapView())
            .SingleOrDefault(gesture => gesture.NumberOfTapsRequired == 2);

        await Assert.That(doubleTap).IsNotNull();

        static IEnumerable<TapGestureRecognizer> mapGestureRecognizers(MapView map) =>
            map.GestureRecognizers.OfType<TapGestureRecognizer>();
    }

    [Test]
    public async Task MapView_refreshes_exactly_the_current_visible_tile_set()
    {
        var source = new RecordingRasterSource
        {
            Handler = (_, _) => Task.FromResult<MapRasterTile?>(null),
        };
        var map = new MapView { TileSource = source };
        map.Measure(512, 512);
        map.Arrange(new Rect(0, 0, 512, 512));

        await map.RefreshTilesAsync();

        await Assert.That(source.Requests).IsEquivalentTo(map.GetVisibleTileKeys());
        await Assert.That(map.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task MapView_source_replacement_drops_superseded_results()
    {
        var firstStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new RecordingRasterSource
        {
            Handler = async (_, cancellationToken) =>
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task.WaitAsync(cancellationToken);
                return (MapRasterTile?)null;
            },
        };
        var second = new RecordingRasterSource
        {
            Handler = (_, _) => Task.FromResult<MapRasterTile?>(null),
        };
        var map = new MapView { TileSource = first };
        map.Measure(512, 512);
        map.Arrange(new Rect(0, 0, 512, 512));

        var firstRefresh = map.RefreshTilesAsync();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        map.TileSource = second;
        releaseFirst.TrySetResult(true);
        await map.RefreshTilesAsync();
        await firstRefresh;

        await Assert.That(second.Requests).IsEquivalentTo(map.GetVisibleTileKeys());
        await Assert.That(map.TileSource).IsSameReferenceAs(second);
    }

    sealed class RecordingRasterSource : IMapRasterSource
    {
        public XyzMapTemplate Template { get; } = new(
            "test-map",
            "https://maps.example/{z}/{x}/{y}.png",
            "Test");

        public List<MapTileKey> Requests { get; } = [];

        public Func<MapTileKey, CancellationToken, Task<MapRasterTile?>>? Handler { get; init; }

        public ValueTask<MapRasterTile?> GetTileAsync(
            MapTileKey key,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(key);
            return Handler is null
                ? ValueTask.FromResult<MapRasterTile?>(null)
                : new ValueTask<MapRasterTile?>(
                    Handler(key, cancellationToken));
        }
    }
}
