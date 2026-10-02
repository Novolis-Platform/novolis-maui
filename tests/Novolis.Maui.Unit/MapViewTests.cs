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
