using Novolis.Maui.PdfViewer;

namespace Novolis.Maui.Unit;

public sealed class PdfZoomLevelTests
{
    [Test]
    public async Task Step_UsesChromiumStylePresets()
    {
        await Assert.That(PdfZoomLevels.Step(1, 1)).IsEqualTo(1.1);
        await Assert.That(PdfZoomLevels.Step(1.1, -1)).IsEqualTo(1);
        await Assert.That(PdfZoomLevels.Step(4, 1)).IsEqualTo(5);
    }

    [Test]
    public async Task Step_ClampsAtReaderBounds()
    {
        await Assert.That(PdfZoomLevels.Step(PdfZoomLevels.Maximum, 1))
            .IsEqualTo(PdfZoomLevels.Maximum);
        await Assert.That(PdfZoomLevels.Step(PdfZoomLevels.Minimum, -1))
            .IsEqualTo(PdfZoomLevels.Minimum);
    }

    [Test]
    public async Task Percent_UsesAStableToolbarLabel()
    {
        await Assert.That(PdfZoomLevels.Percent(1.25)).IsEqualTo("125%");
        await Assert.That(PdfZoomLevels.Percent(7)).IsEqualTo("500%");
    }
}
