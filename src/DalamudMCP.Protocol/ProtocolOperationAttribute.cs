namespace DalamudMCP.Protocol;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ProtocolOperationAttribute(string operationId) : Attribute
{
    public string OperationId { get; } = string.IsNullOrWhiteSpace(operationId)
        ? throw new ArgumentException("Operation id must be non-empty.", nameof(operationId))
        : operationId.Trim();
}
