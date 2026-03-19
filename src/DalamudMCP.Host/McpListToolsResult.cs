namespace DalamudMCP.Host;

public sealed record McpListToolsResult(
    IReadOnlyList<McpListedTool> Tools,
    string? NextCursor);
