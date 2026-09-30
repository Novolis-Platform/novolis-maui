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
        double zoom)
    {
        var paneWidth = Math.Max(160, availableWidth);
        var paneHeight = Math.Max(160, availableHeight);
        var mediaWidth = pageWidth > 0 && double.IsFinite(pageWidth) ? pageWidth : 612;
        var mediaHeight = pageHeight > 0 && double.IsFinite(pageHeight) ? pageHeight : 792;
        var fitted = paneWidth;
        var fittedHeight = fitted * (mediaHeight / mediaWidth);
        if (fittedHeight > paneHeight)
            fitted = paneHeight * (mediaWidth / mediaHeight);

        return Math.Max(160, fitted * Math.Clamp(zoom, 0.5, 4));
    }
}
