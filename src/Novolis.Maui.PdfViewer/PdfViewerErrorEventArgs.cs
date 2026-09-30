namespace Novolis.Maui.PdfViewer;

/// <summary>Viewer error event data.</summary>
public sealed class PdfViewerErrorEventArgs(Exception exception) : EventArgs
{
    /// <summary>Underlying error.</summary>
    public Exception Exception { get; } = exception;
}
