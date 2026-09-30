using Microsoft.Maui.Devices;

namespace Novolis.Maui.PdfViewer;

/// <summary>Chooses phone chrome from the actual screen, not an unconstrained view width.</summary>
public static class PdfViewerLayout
{
    /// <summary>Width below which the reader uses phone chrome.</summary>
    public const double CompactBreakpoint = 800;

    /// <summary>True when the device should use wrapped phone chrome.</summary>
    public static bool IsCompact(double viewWidth = 0)
    {
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

        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            return true;
        return viewWidth > 0 && viewWidth < CompactBreakpoint;
    }
}
