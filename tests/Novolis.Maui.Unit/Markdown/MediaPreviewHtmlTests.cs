using Novolis.Maui.Markdown;

namespace Novolis.Maui.Unit.Markdown;

public sealed class MediaPreviewHtmlTests
{
    [Test]
    public async Task Wrap_EmbedsSvgDataUriWithoutScript()
    {
        const string uri = "data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=";
        var html = MediaPreviewHtml.Wrap(uri, "Mermaid diagram");

        await Assert.That(html).Contains(uri);
        await Assert.That(html).Contains("Mermaid diagram");
        await Assert.That(html).Contains("default-src 'none'");
        await Assert.That(html).Contains("img-src data:");
        await Assert.That(html).DoesNotContain("<script");
    }
}
