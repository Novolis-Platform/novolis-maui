using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiTreeRequestDto(
    [property: Key(0)] long RequestId,
    [property: Key(1)] bool InteractiveOnly = true);
