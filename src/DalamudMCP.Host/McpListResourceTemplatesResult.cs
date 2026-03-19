namespace DalamudMCP.Host;

public sealed record McpListResourceTemplatesResult(
    IReadOnlyList<McpListedResourceTemplate> ResourceTemplates,
    string? NextCursor);
