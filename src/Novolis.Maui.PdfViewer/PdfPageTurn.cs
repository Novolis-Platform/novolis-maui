namespace Novolis.Maui.PdfViewer;

/// <summary>Paging math for a Chromium-style stacked PDF scroll window.</summary>
public static class PdfPageTurn
{
    /// <summary>Number of page slots kept painted around the viewport.</summary>
    public const int WindowSize = 7;

    /// <summary>Pages kept painted on each side of the current page before recycling.</summary>
    public const int KeepAhead = 2;

    /// <summary>Gap between stacked pages, in device-independent pixels.</summary>
    public const double PageGutter = 16;

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

    /// <summary>Page whose top is at or above the given scroll offset (no snap).</summary>
    public static int PageIndexAt(
        double scrollY,
        int windowStart,
        int pageCount,
        double slotHeight)
    {
        if (pageCount <= 0)
            return 0;
        var slot = (int)System.Math.Floor(System.Math.Max(0, scrollY) / System.Math.Max(1, slotHeight));
        return System.Math.Clamp(windowStart + slot, 0, pageCount - 1);
    }

    /// <summary>True when the current page is close enough to the window end that the next page should be painted.</summary>
    public static bool NeedsForwardShift(
        int pageIndex,
        int windowStart,
        int pageCount,
        int windowSize = WindowSize,
        int keepAhead = KeepAhead)
    {
        if (pageCount <= 0 || windowSize <= 0)
            return false;
        var last = windowStart + windowSize - 1;
        if (last >= pageCount - 1)
            return false;
        return pageIndex >= windowStart + windowSize - keepAhead;
    }

    /// <summary>True when the current page is close enough to the window start that the previous page should be painted.</summary>
    public static bool NeedsBackwardShift(
        int pageIndex,
        int windowStart,
        int keepAhead = KeepAhead)
    {
        if (windowStart <= 0)
            return false;
        return pageIndex - windowStart < keepAhead;
    }
}
