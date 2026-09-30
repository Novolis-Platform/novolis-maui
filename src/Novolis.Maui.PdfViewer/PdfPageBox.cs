namespace Novolis.Maui.PdfViewer;

/// <summary>One page in a continuous vertical document, in device-independent pixels.</summary>
public readonly record struct PdfPageBox(int Index, double Top, double Width, double Height);
