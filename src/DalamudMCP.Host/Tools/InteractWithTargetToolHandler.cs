using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class InteractWithTargetToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public InteractWithTargetToolHandler(PluginBridgeClient bridgeClient)
        : base("interact_with_target")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<InteractWithTargetRequest>(arguments)
            ?? new InteractWithTargetRequest(null, null);
        return await bridgeClient.InteractWithTargetAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
