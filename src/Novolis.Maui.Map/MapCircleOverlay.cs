using Microsoft.Maui.Graphics;
using Novolis.Math.Geometry;

namespace Novolis.Maui.Map;

/// <summary>A geographic circle rendered by <see cref="MapView" />.</summary>
public sealed record MapCircleOverlay(
    string Id,
    GeoCircle Circle,
    string? Label = null,
    Color? Ink = null);
