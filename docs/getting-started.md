# Getting started

**novolis-maui** provides MAUI viewers for Markdown and Mermaid that compose `Novolis.Markup.*`. Apps should not reimplement HTML conversion or CSP.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) with the MAUI workload
- GitHub Packages for `Novolis.*` (`https://nuget.pkg.github.com/Novolis-Platform/index.json`)

## Install

```bash
dotnet add package Novolis.Maui.Markdown
```

## Markdown quick start

```csharp
using Novolis.Maui.Markdown;

var view = new MarkdownView
{
    Theme = MarkdownViewTheme.GitHubDark,
    Title = "notes.md",
    Markdown = "# Hello\n\nOpen a `.md` file in your host.",
};

var html = MarkdownHtml.FromMarkdown(markdown, MarkdownViewTheme.StudioDark);
```

## Mermaid quick start

```csharp
using Novolis.Maui.Mermaid;

var view = new MermaidView
{
    DiagramTheme = MermaidViewTheme.StudioDark,
    Source = "flowchart LR\n  A --> B",
};
```

## Local validation

```powershell
dotnet test d:\novolis\novolis-maui\tests\Novolis.Maui.Unit\Novolis.Maui.Unit.csproj -p:NovolisUseProjectReferences=true
```
