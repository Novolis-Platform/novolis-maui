using Novolis.Maui.Agent.Protocol;
using Novolis.Maui.Agent.Protocol.Dto;
using Novolis.Transports.LocalIpc;

namespace Novolis.Maui.Agent;

/// <summary>LocalIpc ui.* host attached to a MAUI page.</summary>
public sealed class AgentHost : IAsyncDisposable
{
    /// <summary>Environment flag that enables the host.</summary>
    public const string EnableEnvVar = "NOVOLIS_MAUI_AGENT";

    private readonly Page _page;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;
    private ILocalIpcListener? _listener;

    private AgentHost(Page page, LocalIpcEndpoint endpoint)
    {
        _page = page;
        _listenTask = Task.Run(() => ListenAsync(endpoint, _cts.Token));
    }

    /// <summary>Attaches using the default MAUI agent pipe.</summary>
    public static AgentHost Attach(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new AgentHost(page, UiTransportEndpoints.CreateDefault());
    }

    /// <summary>Attaches using a dedicated pipe or socket address.</summary>
    public static AgentHost Attach(Page page, string endpointAddress)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointAddress);
        var endpoint = OperatingSystem.IsWindows()
            ? new LocalIpcEndpoint(endpointAddress, LocalIpcTransportKind.NamedPipe)
            : new LocalIpcEndpoint(endpointAddress, LocalIpcTransportKind.UnixDomainSocket);
        return new AgentHost(page, endpoint);
    }

    /// <summary>Attaches when <see cref="EnableEnvVar"/> is set.</summary>
    public static AgentHost? TryAttachFromEnvironment(Page page) =>
        IsEnabledByEnvironment() ? Attach(page) : null;

    /// <summary>True when the host should listen.</summary>
    public static bool IsEnabledByEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(EnableEnvVar);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ListenAsync(LocalIpcEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var marker = Path.Combine(Path.GetTempPath(), "novolis-maui-agent.host");
            await File.WriteAllTextAsync(
                    marker,
                    $"{Environment.ProcessId}{Environment.NewLine}{endpoint.Kind}{Environment.NewLine}{endpoint.Address}{Environment.NewLine}",
                    cancellationToken)
                .ConfigureAwait(false);

            _listener = LocalIpcTransport.CreateListener(endpoint);
            while (!cancellationToken.IsCancellationRequested)
            {
                var connection = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleConnectionAsync(connection, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            CrashGuard.ReportSilent(exception, "AgentHost.listen");
            try
            {
                await File.WriteAllTextAsync(
                        Path.Combine(Path.GetTempPath(), "novolis-maui-agent.host.error"),
                        exception.ToString(),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task HandleConnectionAsync(ILocalIpcConnection connection, CancellationToken cancellationToken)
    {
        await using (connection)
        {
            try
            {
                await foreach (var frame in connection.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (frame.Kind != UiRpcMessageKinds.Request)
                        continue;
                    try
                    {
                        await DispatchAsync(connection, frame, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        CrashGuard.ReportSilent(exception, $"AgentHost.{frame.Name}");
                        try
                        {
                            await ReplyFaultAsync(connection, frame, exception.Message).ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                CrashGuard.ReportSilent(exception, "AgentHost.connection");
            }
        }
    }

    private async Task DispatchAsync(
        ILocalIpcConnection connection,
        LocalIpcFrame frame,
        CancellationToken cancellationToken)
    {
        switch (frame.Name)
        {
            case UiRpcMethodNames.Hello:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        HandleHello(UiProtocolCodec.Deserialize<UiHelloRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Tree:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        HandleTree(UiProtocolCodec.Deserialize<UiTreeRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Screenshot:
                var screenshot = UiProtocolCodec.Deserialize<UiScreenshotRequestDto>(frame.Payload);
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentScreenshot.CaptureAsync(
                            _page,
                            screenshot.ControlId,
                            screenshot.MaxWidth,
                            screenshot.RequestId))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Click:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentInput.Click(_page, UiProtocolCodec.Deserialize<UiClickRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Type:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentInput.Type(_page, UiProtocolCodec.Deserialize<UiTypeRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Select:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentInput.Select(_page, UiProtocolCodec.Deserialize<UiSelectRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Wait:
                await ReplyAsync(
                        connection,
                        frame,
                        await HandleWaitAsync(
                                UiProtocolCodec.Deserialize<UiWaitRequestDto>(frame.Payload),
                                cancellationToken)
                            .ConfigureAwait(false))
                    .ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Get:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentQuery.Get(_page, UiProtocolCodec.Deserialize<UiGetRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Items:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentQuery.Items(_page, UiProtocolCodec.Deserialize<UiItemsRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Focus:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentInput.Focus(_page, UiProtocolCodec.Deserialize<UiFocusRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case UiRpcMethodNames.Scroll:
                await ReplyAsync(connection, frame, await OnUiAsync(() =>
                        AgentInput.Scroll(_page, UiProtocolCodec.Deserialize<UiScrollRequestDto>(frame.Payload)))
                    .ConfigureAwait(false)).ConfigureAwait(false);
                break;
            default:
                await ReplyFaultAsync(connection, frame, $"Unknown method '{frame.Name}'.").ConfigureAwait(false);
                break;
        }
    }

    private static async Task ReplyFaultAsync(ILocalIpcConnection connection, LocalIpcFrame frame, string error)
    {
        object payload = frame.Name switch
        {
            UiRpcMethodNames.Hello => new UiHelloResponseDto(0, false, error, UiProtocolVersion.Current, null, 0),
            UiRpcMethodNames.Tree => new UiTreeResponseDto(0, false, error, []),
            UiRpcMethodNames.Screenshot => new UiScreenshotResponseDto(0, false, error, null, 0, 0),
            UiRpcMethodNames.Click => new UiClickResponseDto(0, false, error, null),
            UiRpcMethodNames.Type => new UiTypeResponseDto(0, false, error),
            UiRpcMethodNames.Select => new UiSelectResponseDto(0, false, error, null, null),
            UiRpcMethodNames.Wait => new UiWaitResponseDto(0, false, error, true),
            UiRpcMethodNames.Get => new UiGetResponseDto(0, false, error, [], null, 0),
            UiRpcMethodNames.Items => new UiItemsResponseDto(0, false, error, "", null, null, []),
            UiRpcMethodNames.Focus => new UiFocusResponseDto(0, false, error, null),
            UiRpcMethodNames.Scroll => new UiScrollResponseDto(0, false, error, null, null),
            _ => new UiHelloResponseDto(0, false, error, UiProtocolVersion.Current, null, 0),
        };
        await ReplyAsync(connection, frame, payload).ConfigureAwait(false);
    }

    private UiHelloResponseDto HandleHello(UiHelloRequestDto request) =>
        new(request.RequestId, true, null, UiProtocolVersion.Current, _page.Title, Environment.ProcessId);

    private UiTreeResponseDto HandleTree(UiTreeRequestDto request) =>
        new(request.RequestId, true, null, AgentTreeWalker.Collect(_page, request.InteractiveOnly));

    private async Task<UiWaitResponseDto> HandleWaitAsync(UiWaitRequestDto request, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(Math.Max(0, request.TimeoutMs));
        while (true)
        {
            var matched = await OnUiAsync(() =>
            {
                var visual = AgentTreeWalker.FindById(_page, request.ControlId);
                if (visual is null)
                    return false;
                if (request.Enabled is bool enabled && visual.IsEnabled != enabled)
                    return false;
                if (request.TextContains is { Length: > 0 } text)
                {
                    var value = AgentTreeWalker.DescribeText(visual);
                    if (value is null || !value.Contains(text, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }).ConfigureAwait(false);

            if (matched)
                return new UiWaitResponseDto(request.RequestId, true, null, false);
            if (DateTime.UtcNow >= deadline)
                return new UiWaitResponseDto(request.RequestId, false, $"Timed out waiting for '{request.ControlId}'.", true);

            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new UiWaitResponseDto(request.RequestId, false, "Cancelled.", true);
            }
        }
    }

    private static async Task ReplyAsync<T>(ILocalIpcConnection connection, LocalIpcFrame frame, T payload) =>
        await connection.SendMessageAsync(frame.Sequence, UiRpcMessageKinds.Response, frame.Name, payload)
            .ConfigureAwait(false);

    private static Task<T> OnUiAsync<T>(Func<T> action) =>
        MainThread.IsMainThread
            ? Task.FromResult(action())
            : MainThread.InvokeOnMainThreadAsync(action);

    private static Task<T> OnUiAsync<T>(Func<Task<T>> action) =>
        MainThread.IsMainThread
            ? action()
            : MainThread.InvokeOnMainThreadAsync(action);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _listenTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        if (_listener is not null)
            await _listener.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
