using Microsoft.Maui.Devices;

namespace Novolis.Maui.PdfViewer;

/// <summary>Chooses phone chrome from the actual screen, not an unconstrained view width.</summary>
public static class PdfViewerLayout
{
    /// <summary>Width below which the reader uses phone chrome.</summary>
    public const double CompactBreakpoint = 800;

    /// <summary>Longest raster edge. Stops a rotate or pinch from allocating a phone-killing bitmap.</summary>
    public const int MaxRasterEdge = 2560;

    /// <summary>True when the device should use wrapped phone chrome.</summary>
    public static bool IsCompact(double viewWidth = 0)
    {
        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            return true;
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop || DeviceInfo.Idiom == DeviceIdiom.TV)
            return viewWidth > 0 && viewWidth < CompactBreakpoint;

        try
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var density = info.Density > 0 ? info.Density : 1;
            var shortest = System.Math.Min(info.Width, info.Height) / density;
            if (shortest > 0 && shortest < CompactBreakpoint)
                return true;
        }
        catch (InvalidOperationException)
        {
        }

        return viewWidth > 0 && viewWidth < CompactBreakpoint;
    }

    /// <summary>Largest page cell in DIP so Android never draws a bitmap past <see cref="MaxRasterEdge"/>.</summary>
    public static double MaxDisplayDip
    {
        get
        {
            var density = Density;
            return density > 1
                ? MaxRasterEdge / density
                : 1200;
        }
    }

    /// <summary>Layout width in device-independent pixels, preferring the live view.</summary>
    public static double WidthDip(double viewWidth = 0) =>
        SanitizeDip(viewWidth, horizontal: true);

    /// <summary>Layout height in device-independent pixels, preferring the live view.</summary>
    public static double HeightDip(double viewHeight = 0) =>
        SanitizeDip(viewHeight, horizontal: false);

    private static double SanitizeDip(double viewSize, bool horizontal)
    {
        var screen = ScreenDip(horizontal);
        if (viewSize > 32)
        {
            // After a rotate MAUI can report pixels. Never let that multiply by density again.
            if (screen > 32 && viewSize > screen * 1.25)
                return screen;
            return viewSize;
        }

        return screen > 32 ? screen : horizontal ? 360 : 640;
    }

    private static double ScreenDip(bool horizontal)
    {
        try
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var density = info.Density > 0 ? info.Density : 1;
            var pixels = horizontal ? info.Width : info.Height;
            var dip = pixels / density;
            return dip > 32 ? dip : 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    /// <summary>Display scale, 1 when the platform has not published a density.</summary>
    public static double Density
    {
        get
        {
            try
            {
                var density = DeviceDisplay.MainDisplayInfo.Density;
                if (density > 0 && double.IsFinite(density))
                    return density;
            }
            catch (InvalidOperationException)
            {
            }

            return 1;
        }
    }

    /// <summary>Device pixels for a DIP cell so Skia paints at the screen density.</summary>
    public static (int Width, int Height) PixelSize(
        double dipWidth,
        double dipHeight,
        double density = 0)
    {
        var scale = density > 0 && double.IsFinite(density) ? density : Density;
        if (scale <= 0 || !double.IsFinite(scale))
            scale = 1;
        var width = (int)System.Math.Round(System.Math.Clamp(dipWidth, 1, 16_384) * scale);
        var height = (int)System.Math.Round(System.Math.Clamp(dipHeight, 1, 16_384) * scale);
        width = System.Math.Clamp(width, 1, 16_384);
        height = System.Math.Clamp(height, 1, 16_384);
        var longest = System.Math.Max(width, height);
        if (longest > MaxRasterEdge)
        {
            var shrink = MaxRasterEdge / (double)longest;
            width = System.Math.Max(1, (int)System.Math.Round(width * shrink));
            height = System.Math.Max(1, (int)System.Math.Round(height * shrink));
        }

        return (width, height);
    }
}
