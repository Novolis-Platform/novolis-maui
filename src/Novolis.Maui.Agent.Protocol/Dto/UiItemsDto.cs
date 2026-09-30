using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiItemsRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string ControlId);

[MessagePackObject]
public sealed record UiItemDto(
    [property: Key(0)] int Index,
    [property: Key(1)] string Text,
    [property: Key(2)] bool Selected);

[MessagePackObject]
public sealed record UiItemsResponseDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool Success,
    [property: Key(2)] string? Error,
    [property: Key(3)] string ControlId,
    [property: Key(4)] string? Kind,
    [property: Key(5)] int? SelectedIndex,
    [property: Key(6)] UiItemDto[] Items);
