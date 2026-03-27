namespace DalamudMCP.Framework.Cli;

public readonly record struct CliInvocationResult(
    object? Result,
    Type ResultType,
    string? Text = null,
    byte[]? RawJsonPayload = null);


