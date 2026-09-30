using Novolis.Maui.PdfViewer;

namespace Novolis.Maui.Unit;

public sealed class PdfPageTurnTests
{
    [Test]
    public async Task WindowStart_KeepsTheCurrentPageInsideASevenSlotWindow()
    {
        await Assert.That(PdfPageTurn.WindowStart(0, 821)).IsEqualTo(0);
        await Assert.That(PdfPageTurn.WindowStart(1, 821)).IsEqualTo(0);
        await Assert.That(PdfPageTurn.WindowStart(0, 2)).IsEqualTo(0);
        await Assert.That(PdfPageTurn.WindowStart(10, 821)).IsEqualTo(7);
        await Assert.That(PdfPageTurn.WindowStart(820, 821)).IsEqualTo(814);
    }

    [Test]
    public async Task SlotOffset_PlacesTheCurrentPageInsideTheWindow()
    {
        await Assert.That(PdfPageTurn.SlotOffset(10, 9, 800)).IsEqualTo(800);
        await Assert.That(PdfPageTurn.SlotOffset(9, 9, 800)).IsEqualTo(0);
    }

    [Test]
    public async Task PageIndexAt_UsesThePageAtTheTopOfTheViewport()
    {
        await Assert.That(PdfPageTurn.PageIndexAt(0, 9, 821, 800)).IsEqualTo(9);
        await Assert.That(PdfPageTurn.PageIndexAt(800, 9, 821, 800)).IsEqualTo(10);
        await Assert.That(PdfPageTurn.PageIndexAt(1200, 9, 821, 800)).IsEqualTo(10);
        await Assert.That(PdfPageTurn.PageIndexAt(1599, 9, 821, 800)).IsEqualTo(10);
        await Assert.That(PdfPageTurn.PageIndexAt(1600, 9, 821, 800)).IsEqualTo(11);
    }

    [Test]
    public async Task NeedsForwardShift_WaitsUntilTheLastPaintedPages()
    {
        await Assert.That(PdfPageTurn.NeedsForwardShift(14, 10, 821)).IsFalse();
        await Assert.That(PdfPageTurn.NeedsForwardShift(15, 10, 821)).IsTrue();
        await Assert.That(PdfPageTurn.NeedsForwardShift(818, 814, 821)).IsFalse();
    }

    [Test]
    public async Task NeedsBackwardShift_WaitsUntilTheFirstPaintedPages()
    {
        await Assert.That(PdfPageTurn.NeedsBackwardShift(12, 10)).IsFalse();
        await Assert.That(PdfPageTurn.NeedsBackwardShift(11, 10)).IsTrue();
        await Assert.That(PdfPageTurn.NeedsBackwardShift(0, 0)).IsFalse();
    }
}
