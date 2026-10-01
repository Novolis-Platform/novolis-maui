using Microsoft.Maui.Graphics;

namespace Novolis.Maui.PdfViewer;

/// <summary>Empty drawable so <see cref="GraphicsView"/> can own one- and two-finger input.</summary>
internal sealed class PdfTouchLayer : IDrawable
{
    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
    }
}
