using Novolis.Maui.Mermaid;

namespace Novolis.Maui.Unit.Mermaid;

public sealed class MermaidHtmlTests
{
    [Test]
    public async Task ToHtmlDocument_EmbedsSvgAndCsp()
    {
        var html = MermaidHtml.ToHtmlDocument("flowchart LR\n  A --> B", MermaidViewTheme.StudioDark);

        await Assert.That(html).Contains("data:image/svg+xml;base64,");
        await Assert.That(html).Contains("default-src 'none'");
        await Assert.That(html).Contains("name=\"viewport\"");
        await Assert.That(html).DoesNotContain("<script");
    }

    [Test]
    public async Task ToHtmlDocument_FallsBackWhenSourceIsInvalid()
    {
        var html = MermaidHtml.ToHtmlDocument("definitely not valid mermaid {{{", MermaidViewTheme.GitHubLight);

        await Assert.That(html).Contains("mermaid-source");
        await Assert.That(html).Contains("definitely not valid mermaid");
    }
}
