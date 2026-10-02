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
        await Assert.That(html).Contains("data-theme=\"dark\"");
        await Assert.That(html).Contains("color-scheme: dark");
        await Assert.That(html).Contains("#0d1117");
        await Assert.That(html).Contains("novolis-md://preview/0");
        await Assert.That(html).Contains("media-preview-open");
    }

    [Test]
    public async Task FromMarkdown_AddsCodeBlockCopyControl()
    {
        var html = MarkdownHtml.FromMarkdown("```csharp\nvar answer = 42;\n```");

        await Assert.That(html).Contains("code-block-copy");
        await Assert.That(html).Contains("novolis-md://copy/0");
        await Assert.That(html).Contains("language-csharp");
    }

    [Test]
    public async Task Build_KeepsCopyPayload()
    {
        var built = MarkdownHtml.Build("```text\nhello\n```");
        await Assert.That(built.Actions.CodeBlocks[0]).Contains("hello");
    }

    [Test]
    public async Task FromMarkdown_EscapesRawHtmlAndKeepsCsp()
    {
        var html = MarkdownHtml.FromMarkdown("<script>alert('nope')</script>", MarkdownViewTheme.GitHubLight);

        await Assert.That(html).Contains("default-src 'none'");
        await Assert.That(html).DoesNotContain("<script>alert");
    }
}
