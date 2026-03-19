using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class AddonCallbackIntToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonCallbackIntToolHandler(PluginBridgeClient bridgeClient)
        : base("send_addon_callback_int")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<AddonCallbackIntRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.AddonName))
        {
            throw new ArgumentException("AddonName is required.", nameof(arguments));
        }

        return await bridgeClient.SendAddonCallbackIntAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
