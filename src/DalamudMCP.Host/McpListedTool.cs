using System.Text.Json;

namespace DalamudMCP.Host;

public sealed record McpListedTool(
    string Name,
    string? Title,
    string? Description,
    JsonElement InputSchema,
    JsonElement? OutputSchema,
    McpToolAnnotations Annotations);
