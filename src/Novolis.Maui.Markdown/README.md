<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-maui">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Maui.Markdown

MAUI Markdown viewer: parse and Mermaid SVG from `Novolis.Markup.*`, display in `Novolis.Maui.WebView`.

## Install

```bash
dotnet add package Novolis.Maui.Markdown
```

## Quick start

```csharp
using Novolis.Maui.Markdown;

var view = new MarkdownView
{
    Theme = MarkdownViewTheme.GitHubDark,
    Title = "architecture.md",
    Markdown = "# Hello\n\n```mermaid\nflowchart LR\n  A --> B\n```",
};

var html = MarkdownHtml.FromMarkdown(markdown, MarkdownViewTheme.StudioDark, title: "Doc");
```

| Type | Role |
| --- | --- |
| `MarkdownView` | Bindable Markdown / Html / Theme |
| `MarkdownHtml` | Headless HTML + CSP for tests and custom hosts |
| `MarkdownViewTheme` | StudioDark / GitHubLight / GitHubDark |

Apps should not reimplement Markdown→HTML. That pipeline lives in `Novolis.Markup.Markdown.Mermaid.Rendering`.
