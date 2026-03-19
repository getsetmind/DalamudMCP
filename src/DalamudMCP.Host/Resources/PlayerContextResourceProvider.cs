namespace DalamudMCP.Host.Resources;

public sealed class PlayerContextResourceProvider : McpResourceProviderBase
{
    private readonly PluginBridgeClient bridgeClient;

    public PlayerContextResourceProvider(PluginBridgeClient bridgeClient)
        : base("ffxiv://player/context")
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

        return await bridgeClient.ReadPlayerContextResourceAsync(cancellationToken).ConfigureAwait(false);
    }
}
