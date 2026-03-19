namespace DalamudMCP.Host;

public sealed record McpInitializeResult(
    string ProtocolVersion,
    McpServerCapabilities Capabilities,
    McpServerInfo ServerInfo,
    string Instructions);
