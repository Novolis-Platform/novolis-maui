<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-maui/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-maui/) · [Source](https://github.com/Novolis-Platform/novolis-maui)
<!-- novolis-pkg-brand:end -->

# Novolis.Maui.Mermaid

MAUI view that renders Mermaid diagrams to SVG via [`Novolis.Markup.Mermaid.Rendering`](https://github.com/Novolis-Platform/novolis-markup) without a browser JavaScript runtime.

## Install

```bash
dotnet add package Novolis.Maui.Mermaid
```

## Quick start

```csharp
using Novolis.Maui.Mermaid;
using Novolis.Markup.Mermaid;

var view = new MermaidView
{
    DiagramTheme = MermaidViewTheme.StudioDark,
    Source = """
        flowchart TD
          A[Start] --> B[Done]
        """,
};
```

| Type | Role |
|------|------|
| `MermaidView` | Bindable `Source` / `Diagram` / `DiagramTheme` |
| `MermaidHtml` | Headless SVG / HTML helpers |
| `MermaidViewTheme` | `StudioDark` or `GitHubLight` |

Pair with `Novolis.Markup.Mermaid` for fluent builders. Markdown fences belong in `Novolis.Maui.Markdown`.
