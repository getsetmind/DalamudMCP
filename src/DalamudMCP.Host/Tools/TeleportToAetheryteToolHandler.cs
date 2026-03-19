using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class TeleportToAetheryteToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public TeleportToAetheryteToolHandler(PluginBridgeClient bridgeClient)
        : base("teleport_to_aetheryte")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<TeleportToAetheryteRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Query is required.", nameof(arguments));
        }

        return await bridgeClient.TeleportToAetheryteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
