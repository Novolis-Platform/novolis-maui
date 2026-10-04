namespace Novolis.Maui.PdfViewer;

/// <summary>Sizes a PDF page so it fits inside the current reading pane.</summary>
public static class PdfPageFit
{
    /// <summary>Returns the display width that fits the page in the pane at the given zoom.</summary>
    public static double PageWidth(
        double availableWidth,
        double availableHeight,
        double pageWidth,
        double pageHeight,
        double zoom,
        PdfFitKind fit = PdfFitKind.Page)
    {
        var size = DisplaySize(availableWidth, availableHeight, pageWidth, pageHeight, zoom, fit);
        return size.Width;
    }

    /// <summary>Pixel size of one page cell at the current fit and zoom.</summary>
    public static (double Width, double Height) DisplaySize(
        double availableWidth,
        double availableHeight,
        double pageWidth,
        double pageHeight,
        double zoom,
        PdfFitKind fit = PdfFitKind.Page,
        double maxEdge = 4096)
    {
        var paneWidth = System.Math.Max(160, availableWidth);
        var paneHeight = System.Math.Max(160, availableHeight);
        var mediaWidth = pageWidth > 0 && double.IsFinite(pageWidth) ? pageWidth : 612;
        var mediaHeight = pageHeight > 0 && double.IsFinite(pageHeight) ? pageHeight : 792;
        var scale = fit == PdfFitKind.Width
            ? paneWidth / mediaWidth
            : System.Math.Min(paneWidth / mediaWidth, paneHeight / mediaHeight);
        scale *= System.Math.Clamp(zoom, PdfZoomLevels.Minimum, PdfZoomLevels.Maximum);
        var width = System.Math.Clamp(mediaWidth * scale, 80, 4096);
        var height = System.Math.Clamp(mediaHeight * scale, 80, 4096);
        var max = maxEdge > 80 ? maxEdge : 4096;
        if (width > max || height > max)
        {
            var shrink = max / System.Math.Max(width, height);
            width *= shrink;
            height *= shrink;
        }

        return (width, height);
    }
}
