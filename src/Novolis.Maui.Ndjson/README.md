<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-maui/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-maui/) · [Source](https://github.com/Novolis-Platform/novolis-maui)
<!-- novolis-pkg-brand:end -->

# Novolis.Maui.Ndjson

MAUI record-slice chrome for [`Novolis.IO.Ndjson`](https://www.nuget.org/packages/Novolis.IO.Ndjson).

`NdjsonSliceView` owns bounded navigation, malformed-record display, expand,
copy, and incremental refresh. The host owns file picking, Windows
associations, Android content-URI materialization, and document lifetime.

```csharp
var view = new NdjsonSliceView
{
    RefreshDocumentAsync = token => session.RefreshAsync(token)
        .ContinueWith(_ => session.Document, token),
};
await view.OpenAsync(document, "events.ndjson");
```

Use `UseGraphicalProfile()` in the executable host.
