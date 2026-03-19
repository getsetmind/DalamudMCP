namespace DalamudMCP.Host.Tools;

public sealed class SessionStatusToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public SessionStatusToolHandler(PluginBridgeClient bridgeClient)
        : base("get_session_status")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        _ = arguments;
        return await bridgeClient.GetSessionStatusAsync(cancellationToken).ConfigureAwait(false);
    }
}
