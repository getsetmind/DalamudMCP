using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Host;

public sealed record McpToolDefinition(
    string ToolName,
    string CapabilityId,
    ProfileType Profile,
    string InputSchemaId,
    string OutputSchemaId,
    string HandlerType,
    bool Experimental,
    string DisplayName,
    string Description);
