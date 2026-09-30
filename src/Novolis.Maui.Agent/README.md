<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-maui">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Maui.Agent

Embeds a LocalIpc `ui.*` agent host in a MAUI page so MCP / tooling can dump the visual tree, screenshot, click, type, select list items, focus, and scroll — the MAUI counterpart of `Novolis.Avalonia.Agent`.

## Install

```bash
dotnet add package Novolis.Maui.Agent
```

## Quick start

```csharp
using Novolis.Maui.Agent;

// After the root page exists:
AgentHost.TryAttachFromEnvironment(mainPage);

// Or always:
AgentHost.Attach(mainPage);

// Dedicated pipe (when multiple MAUI apps may run):
AgentHost.Attach(mainPage, "novolis-maui-agent-pdfreader");
```

Tag controls with `AutomationId` (already used by the walker) or:

```csharp
AgentProperties.SetId(button, "pdf.zoomIn", AgentRoleNames.Button);
```

## Methods

| Method | Purpose |
|--------|---------|
| `ui.hello` | Handshake |
| `ui.get` | Compact multi-id read (text/enabled/visible) |
| `ui.items` | CollectionView / Picker item dump |
| `ui.tree` | Interactive control dump |
| `ui.screenshot` | Window/control PNG |
| `ui.click` | Click by id or coordinates |
| `ui.type` | Type/replace text + special keys |
| `ui.select` | Select CollectionView / Picker by index or item text |
| `ui.focus` | Focus a control by id |
| `ui.scroll` | Scroll nearest `ScrollView` |
| `ui.wait` | Host-side wait for control state |

Enable with env `NOVOLIS_MAUI_AGENT=1`. Optional endpoint override: `NOVOLIS_MAUI_AGENT_ENDPOINT`.

Device / OS smoke (Windows and Android hosts) uses **Novolis.Testing.Appium** — see `d:\novolis\novolis-apps\tests\NovolisPdfReader.UiTests`. Live agent dumps use `MauiAgentDump` or the `MauiAgentMcp` sidecar.
