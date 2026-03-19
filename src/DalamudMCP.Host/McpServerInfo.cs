namespace DalamudMCP.Host;

public sealed record McpServerInfo(
    string Name,
    string Version,
    string? Title,
    string? Description,
    string? WebsiteUrl);
