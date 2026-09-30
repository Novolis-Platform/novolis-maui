using Novolis.Transports.LocalIpc;

namespace Novolis.Maui.Agent.Protocol;

public static class UiIpcConnectionExtensions
{
    public static ValueTask SendMessageAsync<T>(
        this ILocalIpcConnection connection,
        long sequence,
        string kind,
        string name,
        T payload,
        CancellationToken cancellationToken = default) =>
        connection.SendAsync(
            new LocalIpcFrame(sequence, kind, name, UiProtocolCodec.Serialize(payload)),
            cancellationToken);
}
