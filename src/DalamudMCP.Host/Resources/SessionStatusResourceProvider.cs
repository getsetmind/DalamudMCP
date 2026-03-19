namespace DalamudMCP.Host.Resources;

public sealed class SessionStatusResourceProvider : McpResourceProviderBase
{
    private readonly PluginBridgeClient bridgeClient;

    public SessionStatusResourceProvider(PluginBridgeClient bridgeClient)
        : base("ffxiv://session/status")
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

        return await bridgeClient.ReadSessionStatusResourceAsync(cancellationToken).ConfigureAwait(false);
    }
}
