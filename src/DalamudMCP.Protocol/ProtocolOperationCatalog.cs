using MemoryPack;

namespace DalamudMCP.Protocol;

public enum ProtocolOperationVisibility
{
    Both = 0,
    CliOnly = 1,
    McpOnly = 2
}

public enum ProtocolParameterSource
{
    Option = 0,
    Argument = 1
}

public enum ProtocolValueKind
{
    Text = 0,
    Flag = 1,
    Number = 2,
    LargeNumber = 3,
    Real = 4,
    Fixed = 5,
    UniqueId = 6,
    Address = 7,
    Timestamp = 8,
    Json = 9
}

[MemoryPackable]
public sealed partial record ProtocolParameterDescriptor(
    string Name,
    ProtocolValueKind ValueKind,
    ProtocolParameterSource Source,
    bool Required,
    bool IsNullable = false,
    bool IsArray = false,
    int? Position = null,
    string? Description = null,
    IReadOnlyList<string>? Aliases = null,
    string? CliName = null,
    string? McpName = null);

[MemoryPackable]
public sealed partial record ProtocolOperationDescriptor(
    string OperationId,
    ProtocolOperationVisibility Visibility,
    IReadOnlyList<ProtocolParameterDescriptor> Parameters,
    string? Description = null,
    string? Summary = null,
    IReadOnlyList<string>? CliCommandPath = null,
    IReadOnlyList<IReadOnlyList<string>>? CliCommandAliases = null,
    string? McpToolName = null,
    bool Hidden = false);

[MemoryPackable]
[ProtocolOperation("__system.describe-operations")]
public sealed partial record DescribeOperationsRequest;

[MemoryPackable]
public sealed partial record DescribeOperationsResponse(
    IReadOnlyList<ProtocolOperationDescriptor> Operations);

public readonly record struct ProtocolInvocationResult(
    ProtocolPayloadFormat PayloadFormat,
    byte[]? Payload,
    string? DisplayText);
