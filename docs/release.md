# Release

Packages publish from this repo to GitHub Packages on merge to `main`.

See the [Novolis release policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/release-policy.md) for versioning (`2026.1.*`), feed configuration, and consumer `nuget.config` setup.

## Packages

| Package | Project |
|---------|---------|
| `Novolis.Maui.WebView` | `src/Novolis.Maui.WebView` |
| `Novolis.Maui.Mermaid` | `src/Novolis.Maui.Mermaid` |
| `Novolis.Maui.Markdown` | `src/Novolis.Maui.Markdown` |

## Local validation

```powershell
dotnet test d:\novolis\novolis-maui\tests\Novolis.Maui.Unit\Novolis.Maui.Unit.csproj -p:NovolisUseProjectReferences=true
```
