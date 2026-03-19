namespace DalamudMCP.Host.Resources;

public sealed class InventorySummaryResourceProvider : McpResourceProviderBase
{
    private readonly PluginBridgeClient bridgeClient;

    public InventorySummaryResourceProvider(PluginBridgeClient bridgeClient)
        : base("ffxiv://inventory/summary")
    {
        this.bridgeClient = bridgeClient;
    }

    public override bool CanHandle(string uri) =>
        string.Equals(uri, UriTemplate, StringComparison.OrdinalIgnoreCase);

    public override async Task<object> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (!CanHandle(uri))
        {
            throw new InvalidOperationException($"Unsupported resource uri '{uri}'.");
        }

        return await bridgeClient.ReadInventorySummaryResourceAsync(cancellationToken).ConfigureAwait(false);
    }
}
