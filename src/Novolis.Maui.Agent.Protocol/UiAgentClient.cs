using Novolis.Maui.Agent.Protocol.Dto;
using Novolis.Transports.LocalIpc;

namespace Novolis.Maui.Agent.Protocol;

public sealed class UiAgentClient : IAsyncDisposable
{
    private readonly ILocalIpcClient _client;
    private ILocalIpcConnection? _connection;
    private long _sequence;

    public UiAgentClient(ILocalIpcClient? client = null) =>
        _client = client ?? LocalIpcTransport.CreateClient();

    public bool IsConnected => _connection is not null;

    public async ValueTask ConnectAsync(LocalIpcEndpoint endpoint, CancellationToken cancellationToken = default) =>
        _connection = await _client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);

    public ValueTask ConnectDefaultAsync(CancellationToken cancellationToken = default) =>
        ConnectAsync(UiTransportEndpoints.CreateDefault(), cancellationToken);

    public async ValueTask<UiHelloResponseDto> HelloAsync(CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiHelloRequestDto(++_sequence);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Hello, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiHelloResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiTreeResponseDto> TreeAsync(bool interactiveOnly = true, CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiTreeRequestDto(++_sequence, interactiveOnly);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Tree, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiTreeResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiScreenshotResponseDto> ScreenshotAsync(
        string? controlId = null,
        int? maxWidth = null,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiScreenshotRequestDto(++_sequence, controlId, maxWidth);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Screenshot, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiScreenshotResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiClickResponseDto> ClickAsync(
        string? controlId = null,
        double? x = null,
        double? y = null,
        string? button = null,
        int clickCount = 1,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiClickRequestDto(++_sequence, controlId, x, y, button, clickCount);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Click, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiClickResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiTypeResponseDto> TypeAsync(
        string? controlId = null,
        string? text = null,
        string[]? keys = null,
        bool clear = false,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiTypeRequestDto(++_sequence, controlId, text, keys, clear);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Type, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiTypeResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiSelectResponseDto> SelectAsync(
        string controlId,
        int? index = null,
        string? itemText = null,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiSelectRequestDto(++_sequence, controlId, index, itemText);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Select, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiSelectResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiWaitResponseDto> WaitAsync(
        string controlId,
        bool? enabled = null,
        string? textContains = null,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiWaitRequestDto(++_sequence, controlId, enabled, textContains, timeoutMs);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Wait, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiWaitResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiGetResponseDto> GetAsync(
        IEnumerable<string> controlIds,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiGetRequestDto(++_sequence, controlIds.ToArray());
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Get, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiGetResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiItemsResponseDto> ItemsAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiItemsRequestDto(++_sequence, controlId);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Items, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiItemsResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiFocusResponseDto> FocusAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiFocusRequestDto(++_sequence, controlId);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Focus, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiFocusResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<UiScrollResponseDto> ScrollAsync(
        string? controlId = null,
        double? deltaX = null,
        double? deltaY = null,
        bool bringIntoView = false,
        CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        var request = new UiScrollRequestDto(++_sequence, controlId, deltaX, deltaY, bringIntoView);
        await connection.SendMessageAsync(_sequence, UiRpcMessageKinds.Request, UiRpcMethodNames.Scroll, request, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResponseAsync<UiScrollResponseDto>(connection, _sequence, cancellationToken).ConfigureAwait(false);
    }

    private ILocalIpcConnection EnsureConnection() =>
        _connection ?? throw new InvalidOperationException("ConnectAsync must be called before using the UI agent client.");

    private static async ValueTask<TResponse> ReadResponseAsync<TResponse>(
        ILocalIpcConnection connection,
        long expectedSequence,
        CancellationToken cancellationToken)
    {
        await foreach (var frame in connection.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (frame.Sequence != expectedSequence || frame.Kind != UiRpcMessageKinds.Response)
                continue;

            return UiProtocolCodec.Deserialize<TResponse>(frame.Payload);
        }

        throw new EndOfStreamException("The MAUI agent host disconnected before replying.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
