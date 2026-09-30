using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiWaitRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string ControlId,
    [property: Key(2)] bool? Enabled,
    [property: Key(3)] string? TextContains,
    [property: Key(4)] int TimeoutMs = 5000);
