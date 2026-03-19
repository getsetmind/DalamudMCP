using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Host.Tools;

public abstract class McpToolHandlerBase : IMcpToolHandler
{
    protected McpToolHandlerBase(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ToolName = toolName;
    }

    public string ToolName { get; }

    public abstract Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken);

    protected static string ReadAddonName(object? arguments)
    {
        var request = BridgeJson.DeserializePayload<AddonRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.AddonName))
        {
            throw new ArgumentException("AddonName is required.", nameof(arguments));
        }

        return request.AddonName;
    }
}
