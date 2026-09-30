using MessagePack;

namespace Novolis.Maui.Agent.Protocol.Dto;

[MessagePackObject]
public sealed record UiHelloRequestDto(
    [property: Key(0)] long RequestId);
