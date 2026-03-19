namespace DalamudMCP.Host.Resources;

public sealed class AddonCatalogResourceProvider : McpResourceProviderBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonCatalogResourceProvider(PluginBridgeClient bridgeClient)
        : base("ffxiv://ui/addons")
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

        return await bridgeClient.ReadAddonListResourceAsync(cancellationToken).ConfigureAwait(false);
    }
}
