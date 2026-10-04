# Novolis.Maui.Activation

Generic file-activation inbox and pending-publish bridge for MAUI hosts.
Product apps keep their own request types and platform publishers.

## Install

```powershell
dotnet add package Novolis.Maui.Activation
```

## Quick start

```csharp
using Novolis.Maui.Activation;

builder.Services.AddSingleton<MauiActivationInbox<MyOpenRequest>>();
var app = builder.Build();
MauiActivationBridge<MyOpenRequest>.Initialize(
    app.Services.GetRequiredService<MauiActivationInbox<MyOpenRequest>>());

// Platform activation (Windows / Android) may run before the inbox exists:
MauiActivationBridge<MyOpenRequest>.Publish(request);

await foreach (var next in inbox.ReadAllAsync(cancellationToken))
    await OpenAsync(next, cancellationToken);
```

## Support

Shared activation helper; pre-release API.
