<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-maui/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-maui/) · [Source](https://github.com/Novolis-Platform/novolis-maui)
<!-- novolis-pkg-brand:end -->

# Novolis.Maui.Agent.Protocol

MessagePack DTOs, LocalIpc framing helpers, and `UiAgentClient` for the MAUI UI agent RPC protocol (`ui.hello`, `ui.tree`, `ui.screenshot`, `ui.click`, `ui.type`, `ui.select`, `ui.focus`, `ui.scroll`, `ui.wait`, …).

## Install

```bash
dotnet add package Novolis.Maui.Agent.Protocol
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Endpoint

Default named pipe `novolis-maui-agent` (Windows) or temp socket `novolis-maui-agent.sock` (Unix). Override with env `NOVOLIS_MAUI_AGENT_ENDPOINT`.

## Quick start

```csharp
using Novolis.Maui.Agent.Protocol;

await using var client = new UiAgentClient();
await client.ConnectDefaultAsync();
var hello = await client.HelloAsync();
var tree = await client.TreeAsync(interactiveOnly: true);
var shot = await client.ScreenshotAsync(maxWidth: 1280);
```

## API

| API | Purpose |
|-----|---------|
| `UiAgentClient` | Async RPC client over `ILocalIpcClient` |
| `UiAgentClient.ConnectAsync(endpoint)` / `ConnectDefaultAsync()` | Connect to agent host |
| `UiAgentClient.HelloAsync()` | Handshake; returns `UiHelloResponseDto` |
| `UiAgentClient.TreeAsync(interactiveOnly)` | Accessibility/control tree |
| `UiAgentClient.ScreenshotAsync(controlId?, maxWidth?)` | PNG screenshot |
| `UiAgentClient.ClickAsync(controlId?, x?, y?, button?, clickCount)` | Click by id or coordinates |
| `UiAgentClient.TypeAsync(controlId?, text?, keys?, clear)` | Type text or key chords |
| `UiAgentClient.SelectAsync(controlId, index?, itemText?)` | List/combo selection |
| `UiAgentClient.FocusAsync(controlId)` | Focus a control |
| `UiAgentClient.ScrollAsync(controlId?, deltaX?, deltaY?, bringIntoView)` | Scroll / bring into view |
| `UiAgentClient.WaitAsync(controlId, enabled?, textContains?, timeoutMs)` | Wait for control state |
| `UiAgentClient.GetAsync(controlIds)` | Batch read control state |
| `UiAgentClient.ItemsAsync(controlId)` | List items for list/combo controls |
| `UiTransportEndpoints.CreateDefault()` | Platform default pipe/socket endpoint |
| `UiProtocolCodec.Serialize<T>` / `Deserialize<T>` | MessagePack codec |
| `UiProtocolVersion.Current` | Protocol version string (`"1.2"`) |
| `UiRpcMethodNames.*` | RPC method constants (`ui.hello`, `ui.tree`, …) |
| `AgentRoleNames.*` | Semantic roles (`button`, `textbox`, `listbox`, …) |
| `AgentIdAttribute` | Marks controls with stable agent ids |
| `UiTreeNodeDto` / `UiBoundsDto` / `UiControlStateDto` / `UiItemDto` | Core DTO records |

## Related / dogfood

| Package / app | Notes |
|---------------|-------|
| [`Novolis.Maui.Agent`](../Novolis.Maui.Agent/README.md) | Host side — embed agent in a MAUI page |
| [`MauiAgentDump`](../../tools/MauiAgentDump/MauiAgentDump.csproj) | CLI tree/screenshot client |

