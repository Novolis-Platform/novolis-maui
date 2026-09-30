using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiTreeNodeDto(
    [property: Key(0)] string Id,
    [property: Key(1)] string Role,
    [property: Key(2)] string TypeName,
    [property: Key(3)] UiBoundsDto Bounds,
    [property: Key(4)] bool IsEnabled,
    [property: Key(5)] bool IsVisible,
    [property: Key(6)] bool IsFocused,
    [property: Key(7)] string? Text,
    [property: Key(8)] string Path);
