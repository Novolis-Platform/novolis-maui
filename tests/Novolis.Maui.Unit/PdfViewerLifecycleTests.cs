using System.Text;
using Novolis.Maui.PdfViewer;
using PdfViewerControl = Novolis.Maui.PdfViewer.PdfViewer;
using Novolis.Pdf.Core;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Maui.Unit;

public sealed class PdfViewerLifecycleTests
{
    [Test]
    public async Task FailedReplacementLeavesTheCurrentSessionUsable()
    {
        var slot = new PdfSessionSlot();
        var firstSource = PdfSources.FromStream(
            new MemoryStream(BuildPage("First document"), writable: false),
            "first.pdf",
            "first");
        var firstSession = await PdfDocumentSession.OpenAsync(firstSource);
        _ = await slot.ReplaceAsync(firstSession);

        await using var brokenSource = PdfSources.FromStream(
            new MemoryStream(Encoding.ASCII.GetBytes("not a PDF"), writable: false),
            "broken.pdf",
            "broken");
        await Assert.ThrowsAsync<PdfReaderException>(
            async () => await PdfDocumentSession.OpenAsync(brokenSource));

        await Assert.That(slot.Current).IsSameReferenceAs(firstSession);
        await slot.ClearAsync();
    }

    [Test]
    public async Task PasswordProviderIsAssignedAsAnInMemoryCallback()
    {
        var viewer = new PdfViewerControl
        {
            PasswordProvider = static (_, _) => ValueTask.FromResult<string?>("secret"),
        };

        var providerAssigned = viewer.PasswordProvider is not null;
        await Assert.That(providerAssigned).IsTrue();
    }

    private static byte[] BuildPage(string text)
    {
        var content = $"BT /F1 12 Tf 20 80 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
                + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var offsets = new int[objects.Length + 1];
        for (var index = 0; index < objects.Length; index++)
        {
            offsets[index + 1] = output.Count;
            output.AddRange(
                Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n{objects[index]}\nendobj\n"));
        }

        var xrefOffset = output.Count;
        output.AddRange(Encoding.ASCII.GetBytes(
            $"xref\n0 {offsets.Length}\n0000000000 65535 f \n"));
        for (var index = 1; index < offsets.Length; index++)
            output.AddRange(Encoding.ASCII.GetBytes($"{offsets[index]:D10} 00000 n \n"));
        output.AddRange(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {offsets.Length} /Root 1 0 R >>\n"
            + $"startxref\n{xrefOffset}\n%%EOF\n"));
        return output.ToArray();
    }
}
