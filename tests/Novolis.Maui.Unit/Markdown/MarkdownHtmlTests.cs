using Novolis.Maui.Markdown;

namespace Novolis.Maui.Unit.Markdown;

public sealed class MarkdownHtmlTests
{
    [Test]
    public async Task FromMarkdown_RendersMermaidWithoutBrowserScript()
    {
        var html = MarkdownHtml.FromMarkdown(
            "# Architecture\n\n```mermaid\nflowchart LR\n  A --> B\n```",
            MarkdownViewTheme.GitHubDark,
            title: "architecture.md");

        await Assert.That(html).Contains("data:image/svg+xml;base64,");
        await Assert.That(html).DoesNotContain("mermaid.js");
        await Assert.That(html).DoesNotContain("<script");
        await Assert.That(html).Contains("default-src 'none'");
        await Assert.That(html).Contains("architecture.md");
    }

    [Test]
    public async Task FromMarkdown_EscapesRawHtmlAndKeepsCsp()
    {
        var html = MarkdownHtml.FromMarkdown("<script>alert('nope')</script>", MarkdownViewTheme.GitHubLight);

        await Assert.That(html).Contains("default-src 'none'");
        await Assert.That(html).DoesNotContain("<script>alert");
    }
}
