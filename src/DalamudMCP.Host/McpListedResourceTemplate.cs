namespace DalamudMCP.Host;

public sealed record McpListedResourceTemplate(
    string UriTemplate,
    string Name,
    string? Title,
    string? Description,
    string? MimeType);
