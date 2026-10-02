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
}
