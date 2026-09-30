namespace Novolis.Maui.Agent.Protocol;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
public sealed class AgentIdAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
