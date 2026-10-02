using Novolis.Math.Geometry;

namespace Novolis.Maui.Map;

/// <summary>A provider-neutral geographic track rendered as a connected line.</summary>
public sealed record MapTrackOverlay(
    string Id,
    IReadOnlyList<GeoCoordinate> Points,
    string? Label = null);
