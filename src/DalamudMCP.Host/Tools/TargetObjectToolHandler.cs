using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class TargetObjectToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public TargetObjectToolHandler(PluginBridgeClient bridgeClient)
        : base("target_object")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<TargetObjectRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.GameObjectId))
        {
            throw new ArgumentException("GameObjectId is required.", nameof(arguments));
        }

        return await bridgeClient.TargetObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
