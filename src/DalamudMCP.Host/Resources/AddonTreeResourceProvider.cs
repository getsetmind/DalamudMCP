namespace DalamudMCP.Host.Resources;

public sealed class AddonTreeResourceProvider : McpResourceProviderBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonTreeResourceProvider(PluginBridgeClient bridgeClient)
        : base("ffxiv://ui/addon/{addonName}/tree")
    {
        this.bridgeClient = bridgeClient;
    }

    public override bool CanHandle(string uri) =>
        TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/tree", out _);

    public override async Task<object> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (!TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/tree", out var addonName))
        {
            throw new InvalidOperationException($"Unsupported resource uri '{uri}'.");
        }

        return await bridgeClient.ReadAddonTreeResourceAsync(addonName, cancellationToken).ConfigureAwait(false);
    }
}
