using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class MoveToEntityToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public MoveToEntityToolHandler(PluginBridgeClient bridgeClient)
        : base("move_to_entity")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<MoveToEntityRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.GameObjectId))
        {
            throw new ArgumentException("GameObjectId is required.", nameof(arguments));
        }

        return await bridgeClient.MoveToEntityAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
