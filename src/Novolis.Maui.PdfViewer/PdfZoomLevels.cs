namespace Novolis.Maui.PdfViewer;

/// <summary>Preset zoom factors used by the PDF reader toolbar and keyboard.</summary>
public static class PdfZoomLevels
{
    /// <summary>Smallest supported page scale.</summary>
    public const double Minimum = 0.25;

    /// <summary>Largest supported page scale.</summary>
    public const double Maximum = 5;

    /// <summary>
    /// Chromium's stepped zoom model, expressed as factors relative to the
    /// selected fit mode.
    /// </summary>
    public static IReadOnlyList<double> Presets { get; } =
    [
        0.25, 1d / 3d, 0.5, 2d / 3d, 0.75, 0.8, 0.9, 1,
        1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5,
    ];

    /// <summary>Returns the next preset in the requested direction.</summary>
    public static double Step(double current, int direction)
    {
        var value = System.Math.Clamp(current, Minimum, Maximum);
        if (direction == 0)
            return value;

        if (direction > 0)
        {
            foreach (var preset in Presets)
            {
                if (preset > value + 0.005)
                    return preset;
            }

            return Maximum;
        }

        for (var index = Presets.Count - 1; index >= 0; index--)
        {
            if (Presets[index] < value - 0.005)
                return Presets[index];
        }

        return Minimum;
    }

    /// <summary>Formats a factor for the compact reader chrome.</summary>
    public static string Percent(double zoom) =>
        $"{System.Math.Round(System.Math.Clamp(zoom, Minimum, Maximum) * 100):0}%";
}
