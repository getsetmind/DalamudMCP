using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class NearbyInteractablesToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public NearbyInteractablesToolHandler(PluginBridgeClient bridgeClient)
        : base("get_nearby_interactables")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<NearbyInteractablesRequest>(arguments)
            ?? new NearbyInteractablesRequest(null, null, false);
        return await bridgeClient.GetNearbyInteractablesAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
