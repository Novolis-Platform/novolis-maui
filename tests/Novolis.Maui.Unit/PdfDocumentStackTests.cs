using Novolis.Maui.PdfViewer;

namespace Novolis.Maui.Unit;

public sealed class PdfDocumentStackTests
{
    [Test]
    public async Task Layout_StacksPagesWithGutter()
    {
        var boxes = PdfDocumentStack.Layout(
            [(432, 648), (432, 648)],
            paneWidth: 360,
            paneHeight: 640,
            zoom: 1,
            PdfFitKind.Width,
            maxEdge: 1200);
        await Assert.That(boxes.Length).IsEqualTo(2);
        await Assert.That(boxes[0].Top).IsEqualTo(0);
        await Assert.That(boxes[1].Top).IsEqualTo(boxes[0].Height + PdfDocumentStack.Gutter);
        await Assert.That(PdfDocumentStack.DocumentHeight(boxes))
            .IsEqualTo(boxes[1].Top + boxes[1].Height);
    }

    [Test]
    public async Task PageAt_UsesThePageWhoseTopHasPassed()
    {
        var boxes = PdfDocumentStack.Layout(
            [(200, 200), (200, 200), (200, 200)],
            paneWidth: 200,
            paneHeight: 200,
            zoom: 1,
            PdfFitKind.Page,
            maxEdge: 1200);
        await Assert.That(PdfDocumentStack.PageAt(boxes, 0)).IsEqualTo(0);
        await Assert.That(PdfDocumentStack.PageAt(boxes, boxes[1].Top + 1)).IsEqualTo(1);
        await Assert.That(PdfDocumentStack.PageAt(boxes, boxes[2].Top + 40)).IsEqualTo(2);
    }

    [Test]
    public async Task Visible_ReturnsOnlyPagesInTheViewport()
    {
        var boxes = PdfDocumentStack.Layout(
            [(200, 200), (200, 200), (200, 200)],
            paneWidth: 200,
            paneHeight: 200,
            zoom: 1,
            PdfFitKind.Page,
            maxEdge: 1200);
        var visible = PdfDocumentStack.Visible(boxes, boxes[1].Top, 180).ToArray();
        await Assert.That(visible.Length).IsEqualTo(1);
        await Assert.That(visible[0].Index).IsEqualTo(1);
    }

    [Test]
    public async Task ScrollAfterZoom_KeepsTheFocusPointOnTheSamePage()
    {
        var before = PdfDocumentStack.Layout(
            [(200, 400)],
            paneWidth: 200,
            paneHeight: 200,
            zoom: 1,
            PdfFitKind.Width,
            maxEdge: 1200);
        var after = PdfDocumentStack.Layout(
            [(200, 400)],
            paneWidth: 200,
            paneHeight: 200,
            zoom: 2,
            PdfFitKind.Width,
            maxEdge: 1200);
        var scroll = PdfDocumentStack.ScrollAfterZoom(before, after, scrollY: 50, focusY: 100);
        var beforeDoc = 50 + 100;
        var t = (beforeDoc - before[0].Top) / before[0].Height;
        await Assert.That(scroll + 100).IsEqualTo(after[0].Top + (t * after[0].Height)).Within(0.5);
    }

    [Test]
    public async Task ClampScroll_AllowsRestingOnTheLastPage()
    {
        var clamped = PdfDocumentStack.ClampScroll(10_000, documentHeight: 800, viewportHeight: 640);
        await Assert.That(clamped).IsGreaterThan(0);
        await Assert.That(clamped).IsLessThan(800);
    }

    [Test]
    public async Task PageX_CentersAPageThatFits()
    {
        await Assert.That(PdfDocumentStack.PageX(200, 360, scrollX: -80)).IsEqualTo(80);
    }

    [Test]
    public async Task PageX_DoesNotShiftAWidePageOffTheLeftEdge()
    {
        await Assert.That(PdfDocumentStack.PageX(500, 360, scrollX: -400)).IsEqualTo(-140);
        await Assert.That(PdfDocumentStack.PageX(500, 360, scrollX: 20)).IsEqualTo(0);
    }
}
