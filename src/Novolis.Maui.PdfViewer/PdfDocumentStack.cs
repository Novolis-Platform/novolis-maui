namespace Novolis.Maui.PdfViewer;

/// <summary>Chromium-style stacked page geometry. No view recycling.</summary>
public static class PdfDocumentStack
{
    /// <summary>Gap between stacked pages, in DIP.</summary>
    public const double Gutter = 8;

    /// <summary>Lays out every page top-to-bottom at the current fit and zoom.</summary>
    public static PdfPageBox[] Layout(
        IReadOnlyList<(double Width, double Height)> media,
        double paneWidth,
        double paneHeight,
        double zoom,
        PdfFitKind fit,
        double maxEdge)
    {
        var boxes = new PdfPageBox[media.Count];
        var y = 0d;
        for (var i = 0; i < media.Count; i++)
        {
            var size = PdfPageFit.DisplaySize(
                paneWidth,
                paneHeight,
                media[i].Width,
                media[i].Height,
                zoom,
                fit,
                maxEdge);
            boxes[i] = new PdfPageBox(i, y, size.Width, size.Height);
            y += size.Height + Gutter;
        }

        return boxes;
    }

    /// <summary>Document height including the last page, no trailing gutter.</summary>
    public static double DocumentHeight(IReadOnlyList<PdfPageBox> boxes) =>
        boxes.Count == 0 ? 0 : boxes[^1].Top + boxes[^1].Height;

    /// <summary>Page whose top is at or above <paramref name="scrollY"/>.</summary>
    public static int PageAt(IReadOnlyList<PdfPageBox> boxes, double scrollY)
    {
        if (boxes.Count == 0)
            return 0;
        for (var i = boxes.Count - 1; i >= 0; i--)
        {
            if (scrollY + 0.5 >= boxes[i].Top)
                return i;
        }

        return 0;
    }

    /// <summary>Pages that intersect the viewport.</summary>
    public static IEnumerable<PdfPageBox> Visible(
        IReadOnlyList<PdfPageBox> boxes,
        double scrollY,
        double viewportHeight)
    {
        var bottom = scrollY + System.Math.Max(1, viewportHeight);
        foreach (var box in boxes)
        {
            if (box.Top + box.Height < scrollY)
                continue;
            if (box.Top > bottom)
                yield break;
            yield return box;
        }
    }

    /// <summary>Keeps a document-space point under a viewport focus after zoom.</summary>
    public static double ScrollAfterZoom(
        IReadOnlyList<PdfPageBox> before,
        IReadOnlyList<PdfPageBox> after,
        double scrollY,
        double focusY)
    {
        var documentY = scrollY + focusY;
        var index = PageAt(before, documentY);
        if (index >= after.Count || index >= before.Count)
            return 0;
        var old = before[index];
        var next = after[index];
        var t = old.Height <= 0 ? 0 : (documentY - old.Top) / old.Height;
        return next.Top + (t * next.Height) - focusY;
    }

    /// <summary>Clamps scroll so the document can rest with a sliver of the last page on screen.</summary>
    public static double ClampScroll(double scrollY, double documentHeight, double viewportHeight)
    {
        var max = System.Math.Max(0, documentHeight - System.Math.Max(32, viewportHeight * 0.2));
        return System.Math.Clamp(scrollY, 0, max);
    }

    /// <summary>Horizontal origin for a page. Centered when it fits; otherwise clamped so a side stays on screen.</summary>
    public static double PageX(double pageWidth, double viewportWidth, double scrollX)
    {
        var view = System.Math.Max(1, viewportWidth);
        if (pageWidth <= view)
            return (view - pageWidth) * 0.5;
        return System.Math.Clamp(scrollX, view - pageWidth, 0);
    }
}
