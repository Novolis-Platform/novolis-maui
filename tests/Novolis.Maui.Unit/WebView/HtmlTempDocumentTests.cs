using Novolis.Maui.WebView;

namespace Novolis.Maui.Unit.WebView;

public sealed class HtmlTempDocumentTests
{
    [Test]
    public async Task Write_CreatesFileUriThatRoundTrips()
    {
        var uri = HtmlTempDocument.Write("<!DOCTYPE html><html><head></head><body>ok</body></html>");
        try
        {
            await Assert.That(uri).StartsWith("file:");
            await Assert.That(HtmlTempDocument.IsSameDocument(uri, uri)).IsTrue();
            await Assert.That(HtmlTempDocument.IsSameDocument("file:///somewhere-else.html", uri)).IsFalse();
            await Assert.That(Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && File.Exists(parsed.LocalPath)).IsTrue();
        }
        finally
        {
            HtmlTempDocument.TryDelete(uri);
        }
    }
}
