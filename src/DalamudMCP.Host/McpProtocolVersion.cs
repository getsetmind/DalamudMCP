namespace DalamudMCP.Host;

public static class McpProtocolVersion
{
    public const string Current = "2025-11-25";

    public static bool IsSupported(string protocolVersion) =>
        string.Equals(protocolVersion, Current, StringComparison.Ordinal);
}
