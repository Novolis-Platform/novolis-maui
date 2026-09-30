using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiScrollRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string? ControlId,
    [property: Key(2)] double? DeltaX,
    [property: Key(3)] double? DeltaY,
    [property: Key(4)] bool BringIntoView = false);

[MessagePackObject]
public sealed record UiScrollResponseDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool Success,
    [property: Key(2)] string? Error,
    [property: Key(3)] double? OffsetX,
    [property: Key(4)] double? OffsetY);
