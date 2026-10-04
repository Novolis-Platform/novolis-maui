namespace Novolis.Maui.Activation;

/// <summary>
/// Holds activation requests published before the inbox exists, then drains them on initialize.
/// </summary>
/// <typeparam name="T">Product-specific open request.</typeparam>
public static class MauiActivationBridge<T>
{
    private static readonly object Gate = new();
    private static readonly Queue<T> Pending = new();
    private static MauiActivationInbox<T>? inbox;

    /// <summary>Attaches the live inbox and publishes any queued requests.</summary>
    public static void Initialize(MauiActivationInbox<T> activationInbox)
    {
        ArgumentNullException.ThrowIfNull(activationInbox);
        lock (Gate)
        {
            inbox = activationInbox;
            while (Pending.TryDequeue(out var request))
                activationInbox.Publish(request);
        }
    }

    /// <summary>Publishes immediately, or queues until <see cref="Initialize"/>.</summary>
    public static void Publish(T request)
    {
        lock (Gate)
        {
            if (inbox is null)
                Pending.Enqueue(request);
            else
                inbox.Publish(request);
        }
    }
}
