# Novolis.Maui.Map

Provider-neutral MAUI `MapView` for Web Mercator raster tiles and geographic
markers, circles, tracks, and point selection.

The view consumes `Novolis.IO.Maps.IMapRasterSource`. It decodes PNG bytes
inside MAUI and does not reference Avalonia or `Microsoft.Maui.Controls.Maps`.
The surrounding app supplies search fields and other chrome.

```csharp
var map = new MapView
{
    TileSource = source,
    Viewport = new MapViewport(
        new GeoCoordinate(58.14623, 7.99517),
        14),
    Markers =
    [
        new MapMarker(
            "office",
            new GeoCoordinate(58.14623, 7.99517),
            "Office"),
    ],
};

map.PointSelected += coordinate => { };
```

## Install

```bash
dotnet add package Novolis.Maui.Map
```
