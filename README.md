<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-maui.svg" width="100%" alt="novolis-maui"/>
</p>

<p align="center">
  <strong>MAUI chrome for Markdown and Mermaid</strong><br/>
  MAUI viewers that compose Novolis.Markup — the Avalonia-equivalent island.
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-maui/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-maui/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-maui/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-maui"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

---
<!-- novolis-marketing:end -->
<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).
> Open the **package README** for install and quick start — embedded in each .nupkg and linked below.

## Published packages

| Package | Install | Package README |
|---------|---------|----------------|
| `Novolis.Maui.WebView` | `dotnet add package Novolis.Maui.WebView` | [README](https://github.com/Novolis-Platform/novolis-maui/blob/main/src/Novolis.Maui.WebView/README.md) |
| `Novolis.Maui.Mermaid` | `dotnet add package Novolis.Maui.Mermaid` | [README](https://github.com/Novolis-Platform/novolis-maui/blob/main/src/Novolis.Maui.Mermaid/README.md) |
| `Novolis.Maui.Markdown` | `dotnet add package Novolis.Maui.Markdown` | [README](https://github.com/Novolis-Platform/novolis-maui/blob/main/src/Novolis.Maui.Markdown/README.md) |

For NuGet.org and Visual Studio, the **embedded** README.md inside each package is authoritative.

<!-- novolis-package-index:end -->
# novolis-maui

MAUI UI libraries that fill the Avalonia equivalents for Markdown, Mermaid, and locked-down HTML. Parse, theme CSS, and SVG rendering stay in `Novolis.Markup.*`. Apps compose these controls.

| MAUI | Avalonia equivalent | Upstream logic |
|------|---------------------|----------------|
| `Novolis.Maui.Markdown` | `Novolis.Avalonia.Markdown` | `Novolis.Markup.Markdown` + `.Mermaid.Rendering` |
| `Novolis.Maui.Mermaid` | `Novolis.Avalonia.Mermaid` | `Novolis.Markup.Mermaid.Rendering` |
| `Novolis.Maui.WebView` | HtmlRenderer host inside Avalonia preview | CSP + navigation policy |

**MAUI isolation:** only `Novolis.Maui.*` libraries may take `Microsoft.Maui.*` package references (product apps may compose MAUI directly). Do not take Avalonia. `Novolis.Audio.Voice.Platform.Maui` is a grandfathered voice adapter.

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
    Markdown = "# Hello\n\n```mermaid\nflowchart LR\n  A --> B\n```",
};
```

Merglyph is a thin host of these packages (picker, activation, Android/Windows WebView engine lockdown).
