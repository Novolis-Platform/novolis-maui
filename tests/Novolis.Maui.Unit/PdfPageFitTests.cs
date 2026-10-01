using Novolis.Maui.PdfViewer;

namespace Novolis.Maui.Unit;

public sealed class PdfPageFitTests
{
    [Test]
    public async Task FitPage_KeepsLetterInsideThePane()
    {
        var size = PdfPageFit.DisplaySize(900, 700, 612, 792, 1, PdfFitKind.Page);
        await Assert.That(size.Width).IsLessThanOrEqualTo(900);
        await Assert.That(size.Height).IsLessThanOrEqualTo(700);
        await Assert.That(size.Width / size.Height).IsEqualTo(612d / 792d).Within(0.01);
    }

    [Test]
    public async Task FitPage_KeepsSixByNinePortraitInAWidePane()
    {
        var size = PdfPageFit.DisplaySize(2000, 800, 432, 648, 1, PdfFitKind.Page);
        await Assert.That(size.Width).IsLessThan(size.Height);
        await Assert.That(size.Height).IsLessThanOrEqualTo(800);
        await Assert.That(size.Width / size.Height).IsEqualTo(432d / 648d).Within(0.01);
    }

    [Test]
    public async Task FitWidth_UsesPaneWidthAndKeepsSixByNineAspect()
    {
        var size = PdfPageFit.DisplaySize(411, 4000, 432, 648, 1, PdfFitKind.Width);
        await Assert.That(size.Width).IsEqualTo(411).Within(0.5);
        await Assert.That(size.Width / size.Height).IsEqualTo(432d / 648d).Within(0.01);
    }

    [Test]
    public async Task WidthDip_PrefersTheLiveViewWidth()
    {
        await Assert.That(PdfViewerLayout.WidthDip(411)).IsEqualTo(411);
    }

    [Test]
    public async Task ViewDip_KeepsLandscapeWidth()
    {
        await Assert.That(PdfViewerLayout.ViewDip(914, screenDip: 411, screenPixels: 1080))
            .IsEqualTo(914);
    }

    [Test]
    public async Task ViewDip_ScalesAPixelSizedRotateReport()
    {
        await Assert.That(PdfViewerLayout.ViewDip(1080, screenDip: 411, screenPixels: 1080))
            .IsEqualTo(411);
        await Assert.That(PdfViewerLayout.ViewDip(2640, screenDip: 880, screenPixels: 2640))
            .IsEqualTo(880);
    }

    [Test]
    public async Task PixelSize_UsesDisplayDensity()
    {
        var pixels = PdfViewerLayout.PixelSize(360, 540, 3);
        await Assert.That(pixels.Width).IsEqualTo(1080);
        await Assert.That(pixels.Height).IsEqualTo(1620);
    }

    [Test]
    public async Task PixelSize_CapsTheLongEdge()
    {
        var pixels = PdfViewerLayout.PixelSize(880, 1320, 3);
        await Assert.That(System.Math.Max(pixels.Width, pixels.Height))
            .IsLessThanOrEqualTo(PdfViewerLayout.MaxRasterEdge);
        await Assert.That(pixels.Width / (double)pixels.Height)
            .IsEqualTo(880d / 1320d)
            .Within(0.02);
    }

    [Test]
    public async Task DisplaySize_HonorsMaxEdge()
    {
        var size = PdfPageFit.DisplaySize(2640, 1080, 432, 648, 1, PdfFitKind.Width, maxEdge: 853);
        await Assert.That(System.Math.Max(size.Width, size.Height)).IsLessThanOrEqualTo(853);
    }
}
