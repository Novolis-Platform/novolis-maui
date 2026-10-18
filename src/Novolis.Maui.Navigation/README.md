# Novolis.Maui.Navigation

Native MAUI mobile shell for the headless `Novolis.Commands.Navigation`
menu-tree contract.

## Install

```powershell
dotnet add package Novolis.Maui.Navigation
```

## Quick start

```csharp
var navigation = new MobileNavigationController(menuTree);
var shell = new MobileNavigationShell(navigation)
{
    Title = "Presence Ledger",
    Body = pageContent,
};
shell.ActivationRequested += (_, activation) =>
{
    if (activation.RouteId is { } route)
        ShowRoute(route);
    else if (activation.CommandId is { } command)
        RunCommand(command);
};
```

The shell provides the native mobile composition: app bar, Back, persistent
primary navigation, secondary menu surface, touch targets, and accessibility
descriptions. Route content and command execution remain in the host app.

Both the MAUI and Avalonia renderers consume the same menu tree and controller.
They use platform-native controls while sharing the Novolis Graphical Profile
geometry and semantic color roles.

## Related packages

- `Novolis.Commands.Navigation`
- `Novolis.Avalonia.Navigation`
