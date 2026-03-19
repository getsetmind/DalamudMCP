namespace DalamudMCP.Host.Resources;

public sealed class AddonStringsResourceProvider : McpResourceProviderBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonStringsResourceProvider(PluginBridgeClient bridgeClient)
        : base("ffxiv://ui/addon/{addonName}/strings")
    {
        this.bridgeClient = bridgeClient;
    }

    public override bool CanHandle(string uri) =>
        TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/strings", out _);

    public override async Task<object> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (!TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/strings", out var addonName))
        {
            throw new InvalidOperationException($"Unsupported resource uri '{uri}'.");
        }

        return await bridgeClient.ReadAddonStringsResourceAsync(addonName, cancellationToken).ConfigureAwait(false);
    }
}
