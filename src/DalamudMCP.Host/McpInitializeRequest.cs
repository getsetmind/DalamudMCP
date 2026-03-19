namespace DalamudMCP.Host;

public sealed record McpInitializeRequest(
    string ProtocolVersion,
    McpClientCapabilities Capabilities,
    McpClientInfo ClientInfo);
