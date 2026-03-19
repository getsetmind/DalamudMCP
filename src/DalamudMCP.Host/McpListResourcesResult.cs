namespace DalamudMCP.Host;

public sealed record McpListResourcesResult(
    IReadOnlyList<McpListedResource> Resources,
    string? NextCursor);
