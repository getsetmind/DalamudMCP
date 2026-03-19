namespace DalamudMCP.Host;

public sealed record McpClientInfo(
    string Name,
    string Version,
    string? Title = null);
