using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public sealed class AddonCallbackValuesToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public AddonCallbackValuesToolHandler(PluginBridgeClient bridgeClient)
        : base("send_addon_callback_values")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        var request = BridgeJson.DeserializePayload<AddonCallbackValuesRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.AddonName))
        {
            throw new ArgumentException("AddonName is required.", nameof(arguments));
        }

        if (request.Values is null || request.Values.Length == 0)
        {
            throw new ArgumentException("At least one callback value is required.", nameof(arguments));
        }

        return await bridgeClient.SendAddonCallbackValuesAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
