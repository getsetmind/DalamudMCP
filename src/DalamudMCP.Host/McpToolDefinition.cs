namespace DalamudMCP.Host;

public sealed record McpToolDefinition(
    string ToolName,
    string CapabilityId,
    string InputSchemaId,
    string OutputSchemaId,
    string HandlerType,
    bool Experimental,
    string DisplayName,
    string Description);
