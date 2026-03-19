namespace DalamudMCP.Host.Tools;

public sealed class PlayerContextToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public PlayerContextToolHandler(PluginBridgeClient bridgeClient)
        : base("get_player_context")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        _ = arguments;
        return await bridgeClient.GetPlayerContextAsync(cancellationToken).ConfigureAwait(false);
    }
}
