namespace Novolis.Maui.PdfViewer;

/// <summary>How a page is scaled into the reading pane before zoom.</summary>
public enum PdfFitKind
{
    /// <summary>The whole page is visible in the pane.</summary>
    Page,

    /// <summary>The page width matches the pane; height may scroll.</summary>
    Width,
}
