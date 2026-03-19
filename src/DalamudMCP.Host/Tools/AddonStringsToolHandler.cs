namespace DalamudMCP.Host.Tools;

public sealed class AddonStringsToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonStringsToolHandler(PluginBridgeClient bridgeClient)
        : base("get_addon_strings")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken) =>
        await bridgeClient.GetAddonStringsAsync(ReadAddonName(arguments), cancellationToken).ConfigureAwait(false);
}
