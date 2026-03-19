namespace DalamudMCP.Host.Tools;

public sealed class AddonTreeToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonTreeToolHandler(PluginBridgeClient bridgeClient)
        : base("get_addon_tree")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken) =>
        await bridgeClient.GetAddonTreeAsync(ReadAddonName(arguments), cancellationToken).ConfigureAwait(false);
}
