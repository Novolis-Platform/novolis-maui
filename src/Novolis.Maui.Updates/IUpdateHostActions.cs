using Novolis.Registry.Updates;

namespace Novolis.Maui.Updates;

/// <summary>Host seams for notifications, browser, Downloads, and package handoff.</summary>
public interface IUpdateHostActions
{
    /// <summary>Shows a lightweight notification.</summary>
    ValueTask ShowToastAsync(UpdateSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Shows a prominent update prompt.</summary>
    ValueTask ShowPopupAsync(UpdateSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Opens the public GitHub release page.</summary>
    ValueTask OpenReleaseAsync(Uri releaseUri, CancellationToken cancellationToken = default);

    /// <summary>Reveals a verified artifact in the Downloads surface.</summary>
    ValueTask RevealDownloadAsync(string path, CancellationToken cancellationToken = default);
}
