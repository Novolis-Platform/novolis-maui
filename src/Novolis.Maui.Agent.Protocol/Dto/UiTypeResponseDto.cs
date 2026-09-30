using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiTypeResponseDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool Success,
    [property: Key(2)] string? Error);
