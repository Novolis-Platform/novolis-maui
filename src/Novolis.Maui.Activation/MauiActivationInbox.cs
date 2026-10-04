using System.Threading.Channels;

namespace Novolis.Maui.Activation;

/// <summary>Single-reader channel that delivers activation requests to the host page.</summary>
/// <typeparam name="T">Product-specific open request.</typeparam>
public sealed class MauiActivationInbox<T>
{
    private readonly Channel<T> channel = Channel.CreateUnbounded<T>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>Enqueues a request. Returns false if the inbox has completed.</summary>
    public bool Publish(T request) => channel.Writer.TryWrite(request);

    /// <summary>Reads requests until the inbox is completed or cancelled.</summary>
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}
