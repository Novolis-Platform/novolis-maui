using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiBoundsDto(
    [property: Key(0)] double X,
    [property: Key(1)] double Y,
    [property: Key(2)] double Width,
    [property: Key(3)] double Height);
