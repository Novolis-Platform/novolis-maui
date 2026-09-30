using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiClickRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] string? ControlId,
    [property: Key(2)] double? X,
    [property: Key(3)] double? Y,
    /// <summary>left (default), right, or middle.</summary>
    [property: Key(4)] string? Button = null,
    /// <summary>1 = single click (default); 2 = double-click. Missing/0 treated as 1.</summary>
    [property: Key(5)] int ClickCount = 1);
