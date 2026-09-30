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
}
