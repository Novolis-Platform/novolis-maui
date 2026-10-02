using Microsoft.Maui.Graphics;
using Novolis.Math.Geometry;

namespace Novolis.Maui.Map;

/// <summary>A selectable point rendered by <see cref="MapView" />.</summary>
public sealed record MapMarker(
    string Id,
    GeoCoordinate Position,
    string? Label = null,
    double RadiusPixels = 6,
    Color? Ink = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    object? Tag = null);
