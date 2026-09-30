using Novolis.Maui.Agent.Protocol;
using Novolis.Maui.Agent.Protocol.Dto;

namespace Novolis.Maui.Unit;

public sealed class MauiAgentProtocolTests
{
    [Test]
    public async Task RoundTripsHelloPayload()
    {
        var original = new UiHelloResponseDto(7, true, null, UiProtocolVersion.Current, "Novolis PDF Reader", 42);
        var copy = UiProtocolCodec.Deserialize<UiHelloResponseDto>(UiProtocolCodec.Serialize(original));

        await Assert.That(copy.RequestId).IsEqualTo(7);
        await Assert.That(copy.Success).IsTrue();
        await Assert.That(copy.AppTitle).IsEqualTo("Novolis PDF Reader");
        await Assert.That(copy.ProcessId).IsEqualTo(42);
    }

    [Test]
    public async Task UsesMauiAgentPipeName()
    {
        await Assert.That(UiTransportEndpoints.DefaultPipeName).IsEqualTo("novolis-maui-agent");
        await Assert.That(UiRpcMethodNames.Tree).IsEqualTo("ui.tree");
    }
}
