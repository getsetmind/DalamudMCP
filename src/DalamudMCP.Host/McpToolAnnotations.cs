namespace DalamudMCP.Host;

public sealed record McpToolAnnotations(
    string? Title,
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);
