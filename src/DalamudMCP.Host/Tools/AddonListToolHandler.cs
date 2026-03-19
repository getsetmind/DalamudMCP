namespace DalamudMCP.Host.Tools;

public sealed class AddonListToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonListToolHandler(PluginBridgeClient bridgeClient)
        : base("get_addon_list")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        _ = arguments;
        return await bridgeClient.GetAddonListAsync(cancellationToken).ConfigureAwait(false);
    }
}
