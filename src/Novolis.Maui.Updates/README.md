# Novolis.Maui.Updates

MAUI update status, notification, and platform-handoff controls for
direct-release applications. The component is deliberately platform-neutral;
the product host supplies toast/popup, file reveal, and installer/package
handoff behavior.

## Install

```powershell
dotnet add package Novolis.Maui.Updates
```

Requires .NET 10, `Microsoft.Maui.Controls`, `Novolis.Registry.Updates`, and
`Novolis.Maui.GraphicalProfile`.

## Quick start

```csharp
using Novolis.Maui.Updates;

var updateView = new UpdateStatusView
{
    Coordinator = coordinator,
    HostActions = hostActions,
    NotificationMode = UpdateNotificationMode.Toast,
};
```

Configure `coordinator` with the neutral registry updater and a
`Novolis.Registry.GitHub` source. `hostActions` opens the release URL, presents
inline/toast/popup notifications, reveals downloaded files, and hands verified
artifacts to the product's platform installer. Set `NotificationMode` to
`Inline`, `Toast`, or `Popup`.

The view uses Graphical Profile dynamic resources, main-thread-safe rendering,
and stable automation IDs such as `UpdateStatusView`,
`UpdateStatusView.CheckButton`, and `UpdateStatusView.DownloadButton`.

## Related packages

| Package | Role |
| --- | --- |
| `Novolis.Registry.Updates` | Polling, state, verification, and handoff contracts |
| `Novolis.Registry.GitHub` | GitHub Releases update source |
| `Novolis.Maui.GraphicalProfile` | Shared light/dark shell resources |

## Support

Direct-release updater component; pre-release API.
