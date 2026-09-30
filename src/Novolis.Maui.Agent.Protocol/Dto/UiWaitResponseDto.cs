using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiWaitResponseDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool Success,
    [property: Key(2)] string? Error,
    [property: Key(3)] bool TimedOut);
