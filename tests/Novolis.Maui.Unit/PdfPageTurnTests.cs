using Novolis.Maui.PdfViewer;

namespace Novolis.Maui.Unit;

public sealed class PdfPageTurnTests
{
    [Test]
    public async Task WindowStart_KeepsTheCurrentPageInsideAThreeSlotWindow()
    {
        await Assert.That(PdfPageTurn.WindowStart(0, 821)).IsEqualTo(0);
        await Assert.That(PdfPageTurn.WindowStart(1, 821)).IsEqualTo(0);
        await Assert.That(PdfPageTurn.WindowStart(0, 2)).IsEqualTo(0);
        await Assert.That(PdfPageTurn.WindowStart(10, 821)).IsEqualTo(9);
        await Assert.That(PdfPageTurn.WindowStart(820, 821)).IsEqualTo(818);
    }

    [Test]
    public async Task SlotOffset_PlacesTheCurrentPageInsideTheWindow()
    {
        await Assert.That(PdfPageTurn.SlotOffset(10, 9, 800)).IsEqualTo(800);
        await Assert.That(PdfPageTurn.SlotOffset(9, 9, 800)).IsEqualTo(0);
    }

    [Test]
    public async Task PageIndexAt_MapsScrollToTheNearestPage()
    {
        await Assert.That(PdfPageTurn.PageIndexAt(0, 9, 821, 800)).IsEqualTo(9);
        await Assert.That(PdfPageTurn.PageIndexAt(800, 9, 821, 800)).IsEqualTo(10);
        await Assert.That(PdfPageTurn.PageIndexAt(1200, 9, 821, 800)).IsEqualTo(11);
    }

    [Test]
    public async Task SnapOffset_LocksToASlot()
    {
        await Assert.That(PdfPageTurn.SnapOffset(850, 800)).IsEqualTo(800);
        await Assert.That(PdfPageTurn.SnapOffset(399, 800)).IsEqualTo(0);
    }
}
