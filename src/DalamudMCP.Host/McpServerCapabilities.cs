namespace DalamudMCP.Host;

public sealed record McpServerCapabilities(
    McpToolsCapability? Tools,
    McpResourcesCapability? Resources);

public sealed record McpToolsCapability(bool ListChanged);

public sealed record McpResourcesCapability(bool Subscribe, bool ListChanged);
