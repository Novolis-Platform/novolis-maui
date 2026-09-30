using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiSelectRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string ControlId,
    [property: Key(2)] int? Index = null,
    [property: Key(3)] string? ItemText = null);
