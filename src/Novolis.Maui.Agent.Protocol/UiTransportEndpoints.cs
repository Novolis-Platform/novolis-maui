using Novolis.Transports.LocalIpc;

namespace Novolis.Maui.Agent.Protocol;

public static class UiTransportEndpoints
{
    public const string DefaultPipeName = "novolis-maui-agent";
    public const string EndpointEnvVar = "NOVOLIS_MAUI_AGENT_ENDPOINT";

    public static LocalIpcEndpoint CreateDefault()
    {
        var overrideAddress = Environment.GetEnvironmentVariable(EndpointEnvVar);
        if (!string.IsNullOrWhiteSpace(overrideAddress))
        {
            return OperatingSystem.IsWindows()
                ? new LocalIpcEndpoint(overrideAddress, LocalIpcTransportKind.NamedPipe)
                : new LocalIpcEndpoint(overrideAddress, LocalIpcTransportKind.UnixDomainSocket);
        }

        if (OperatingSystem.IsWindows())
            return new LocalIpcEndpoint(DefaultPipeName, LocalIpcTransportKind.NamedPipe);

        var socketPath = Path.Combine(Path.GetTempPath(), DefaultPipeName + ".sock");
        return new LocalIpcEndpoint(socketPath, LocalIpcTransportKind.UnixDomainSocket);
    }
}
