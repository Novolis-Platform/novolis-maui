using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiTypeRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string? ControlId,
    [property: Key(2)] string? Text,
    [property: Key(3)] string[]? Keys,
    [property: Key(4)] bool Clear = false);
