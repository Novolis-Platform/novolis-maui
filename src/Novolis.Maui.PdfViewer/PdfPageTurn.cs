namespace Novolis.Maui.PdfViewer;

/// <summary>Paging math for a small window of stacked PDF pages.</summary>
public static class PdfPageTurn
{
    /// <summary>Number of page slots kept in the reading scroll window.</summary>
    public const int WindowSize = 3;

    /// <summary>Returns the first page index of a window centered on the current page.</summary>
    public static int WindowStart(int pageIndex, int pageCount, int windowSize = WindowSize)
    {
        if (pageCount <= 0 || windowSize <= 0)
            return 0;
        if (pageCount <= windowSize)
            return 0;
        var start = pageIndex - (windowSize / 2);
        return System.Math.Clamp(start, 0, pageCount - windowSize);
    }

    /// <summary>Scroll offset of a page inside the current window.</summary>
    public static double SlotOffset(int pageIndex, int windowStart, double slotHeight) =>
        System.Math.Max(0, pageIndex - windowStart) * System.Math.Max(1, slotHeight);

    /// <summary>Page index nearest to a scroll offset.</summary>
    public static int PageIndexAt(
        double scrollY,
        int windowStart,
        int pageCount,
        double slotHeight)
    {
        if (pageCount <= 0)
            return 0;
        var slot = (int)System.Math.Round(scrollY / System.Math.Max(1, slotHeight));
        return System.Math.Clamp(windowStart + slot, 0, pageCount - 1);
    }

    /// <summary>Snaps a scroll offset to the nearest page slot.</summary>
    public static double SnapOffset(double scrollY, double slotHeight)
    {
        var height = System.Math.Max(1, slotHeight);
        var slot = (int)System.Math.Round(scrollY / height);
        return System.Math.Max(0, slot) * height;
    }
}
