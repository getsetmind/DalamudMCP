namespace DalamudMCP.Host;

public sealed record McpClientCapabilities(
    bool ToolsListChanged,
    bool ResourcesSubscribe,
    bool ResourcesListChanged);
