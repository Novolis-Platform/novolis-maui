using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiGetRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string[] ControlIds);

[MessagePackObject]
public sealed record UiControlStateDto(
    [property: Key(0)] string Id,
    [property: Key(1)] bool Found,
    [property: Key(2)] bool IsEnabled,
    [property: Key(3)] bool IsVisible,
    [property: Key(4)] string? Text,
    [property: Key(5)] string? Role,
    [property: Key(6)] string? TypeName);

[MessagePackObject]
public sealed record UiGetResponseDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool Success,
    [property: Key(2)] string? Error,
    [property: Key(3)] UiControlStateDto[] Controls,
    [property: Key(4)] string? AppTitle,
    [property: Key(5)] int ProcessId);
