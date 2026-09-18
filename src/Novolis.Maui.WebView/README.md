<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-maui">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Maui.WebView

Locked-down MAUI `WebView` for locally rendered HTML. Apps compose this instead of wiring CSP and navigation cancel themselves.

## Install

```bash
dotnet add package Novolis.Maui.WebView
```

## Quick start

```csharp
using Novolis.Maui.WebView;

var html = HtmlContentSecurityPolicy.Apply(documentHtml);
var view = new SecureHtmlWebView { Html = html };
```

| Type | Role |
| --- | --- |
| `HtmlContentSecurityPolicy` | Insert restrictive CSP meta (`img-src data:`, no script) |
| `WebViewNavigationPolicy` | Classify in-view vs external URIs |
| `SecureHtmlWebView` | WebView that never follows in-page links |

Engine lockdown (disable JS on Android/Windows WebView2) stays in the product host until this package packs platform TFMs.

## Related

- `Novolis.Maui.Markdown` — Markdown viewer on this WebView
- `Novolis.Avalonia.Markdown` — Avalonia equivalent (HtmlRenderer, not WebView)
