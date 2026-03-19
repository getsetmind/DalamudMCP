namespace DalamudMCP.Host;

public sealed record McpListedResource(
    string Uri,
    string Name,
    string? Title,
    string? Description,
    string? MimeType);
