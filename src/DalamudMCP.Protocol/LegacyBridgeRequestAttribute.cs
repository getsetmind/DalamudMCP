namespace DalamudMCP.Protocol;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class LegacyBridgeRequestAttribute(string requestType) : Attribute
{
    public string RequestType { get; } = string.IsNullOrWhiteSpace(requestType)
        ? throw new ArgumentException("Request type must be non-empty.", nameof(requestType))
        : requestType.Trim();
}
