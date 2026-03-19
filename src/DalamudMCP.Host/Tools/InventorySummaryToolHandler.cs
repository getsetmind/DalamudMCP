namespace DalamudMCP.Host.Tools;

public sealed class InventorySummaryToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public InventorySummaryToolHandler(PluginBridgeClient bridgeClient)
        : base("get_inventory_summary")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        _ = arguments;
        return await bridgeClient.GetInventorySummaryAsync(cancellationToken).ConfigureAwait(false);
    }
}
