using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiHelloResponseDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool Success,
    [property: Key(2)] string? Error,
    [property: Key(3)] string ProtocolVersion,
    [property: Key(4)] string? AppTitle,
    [property: Key(5)] int ProcessId);
