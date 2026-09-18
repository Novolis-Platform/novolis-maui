# Design

## Purpose

MAUI UI island parallel to `Novolis.Avalonia.*` for Markdown/Mermaid viewers. General logic stays upstream in `Novolis.Markup.*`. Apps compose standardized controls.

## Packages

| Package | Responsibility |
|---------|----------------|
| `Novolis.Maui.WebView` | CSP insertion, navigation policy, `SecureHtmlWebView` |
| `Novolis.Maui.Mermaid` | `MermaidView` + SVG HTML helpers over Markup.Mermaid.Rendering |
| `Novolis.Maui.Markdown` | `MarkdownView` + themed HTML over Markup.Markdown.Mermaid.Rendering |

## Layer

Orthogonal to the closed spine (Math → … → Avalonia). **MAUI isolation:** only `Novolis.Maui.*` libraries may take `Microsoft.Maui.*` (product apps may compose MAUI). Must not reference Avalonia. Avalonia must not reference MAUI.

Grandfathered adapter: `Novolis.Audio.Voice.Platform.Maui` (TTS speak-only). New MAUI UI belongs here, not in Audio.

## Composition

```text
Novolis.Markup.Markdown / Mermaid / Rendering
        ↓
Novolis.Maui.WebView / Mermaid / Markdown
        ↓
Apps (Merglyph picker, activation, platform WebView engine lockdown)
```

Avalonia hosts use the same Markup packages via `Novolis.Avalonia.Markdown` / `.Mermaid`.

Libraries target `net10.0` with `Microsoft.Maui.Controls` — not `UseMaui` app TFMs — so pack/test on the same Linux merge pipeline as Avalonia. Product hosts (Merglyph) keep `UseMaui` and platform frameworks.

## Non-goals

- Product hosts or `apps/` in this repo
- Re-parsing Markdown in the MAUI layer
- Pulling Avalonia into these packages
- Local NuGet folder feeds
