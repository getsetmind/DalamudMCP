using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Host.Tools;

namespace DalamudMCP.Host;

public sealed class McpToolInvoker
{
    private readonly Func<string, string, CancellationToken, Task>? auditRecorder;
    private readonly Func<CancellationToken, Task<CapabilityStateResponse>>? capabilityStateProvider;
    private readonly Dictionary<string, IMcpToolHandler> handlersByName;
    private readonly McpToolRegistry toolRegistry;

    public McpToolInvoker(PluginBridgeClient bridgeClient, McpToolRegistry toolRegistry)
        : this(CreateDefaultHandlers(bridgeClient), toolRegistry, bridgeClient.GetCapabilityStateAsync, bridgeClient.RecordAuditEventAsync)
    {
    }

    public McpToolInvoker(
        IEnumerable<IMcpToolHandler> handlers,
        McpToolRegistry toolRegistry,
        Func<CancellationToken, Task<CapabilityStateResponse>>? capabilityStateProvider,
        Func<string, string, CancellationToken, Task>? auditRecorder = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        this.toolRegistry = toolRegistry;
        this.capabilityStateProvider = capabilityStateProvider;
        this.auditRecorder = auditRecorder;
        handlersByName = handlers.ToDictionary(static handler => handler.ToolName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<object> InvokeAsync(string toolName, object? arguments, CancellationToken cancellationToken)
    {
        if (toolRegistry.IsDenied(toolName))
        {
            await TryRecordDeniedAsync("tool.request_denied", $"tool={toolName}; reason=capability_denied", cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Tool '{toolName}' is denied.");
        }

        if (!toolRegistry.TryGet(toolName, out _))
        {
            throw new InvalidOperationException($"Unknown tool '{toolName}'.");
        }

        var capabilityState = await GetCapabilityStateAsync(cancellationToken).ConfigureAwait(false);
        if (!IsToolEnabled(toolName, capabilityState))
        {
            throw new InvalidOperationException($"Tool '{toolName}' is not enabled.");
        }

        if (!IsAddonAllowed(toolName, arguments, capabilityState))
        {
            await TryRecordDeniedAsync("tool.request_denied", $"tool={toolName}; reason=addon_denied_or_disabled", cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Addon access for tool '{toolName}' is not enabled.");
        }

        if (!handlersByName.TryGetValue(toolName, out var handler))
        {
            throw new InvalidOperationException($"Tool '{toolName}' is not implemented.");
        }

        return await handler.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CapabilityStateResponse?> GetCapabilityStateAsync(CancellationToken cancellationToken)
    {
        if (capabilityStateProvider is null)
        {
            return null;
        }

        return await capabilityStateProvider(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsToolEnabled(string toolName, CapabilityStateResponse? capabilityState)
    {
        if (capabilityState is null)
        {
            return true;
        }

        if (!capabilityState.EnabledTools.Contains(toolName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(toolName, "target_object", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "interact_with_target", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "move_to_entity", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "send_addon_callback_int", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "send_addon_callback_values", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "teleport_to_aetheryte", StringComparison.OrdinalIgnoreCase)
            ? capabilityState.ActionProfileEnabled
            : capabilityState.ObservationProfileEnabled;
    }

    private bool IsAddonAllowed(string toolName, object? arguments, CapabilityStateResponse? capabilityState)
    {
        if (capabilityState is null)
        {
            return true;
        }

        if (!string.Equals(toolName, "get_addon_tree", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(toolName, "get_addon_strings", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var request = BridgeJson.DeserializePayload<AddonRequest>(arguments);
        if (request is null || string.IsNullOrWhiteSpace(request.AddonName))
        {
            return true;
        }

        if (toolRegistry.IsDeniedAddon(request.AddonName))
        {
            return false;
        }

        return capabilityState.EnabledAddons.Contains(request.AddonName, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IMcpToolHandler> CreateDefaultHandlers(PluginBridgeClient bridgeClient) =>
        [
            new SessionStatusToolHandler(bridgeClient),
            new PlayerContextToolHandler(bridgeClient),
            new DutyContextToolHandler(bridgeClient),
            new InventorySummaryToolHandler(bridgeClient),
            new AddonListToolHandler(bridgeClient),
            new AddonTreeToolHandler(bridgeClient),
            new AddonStringsToolHandler(bridgeClient),
            new NearbyInteractablesToolHandler(bridgeClient),
            new TargetObjectToolHandler(bridgeClient),
            new InteractWithTargetToolHandler(bridgeClient),
            new MoveToEntityToolHandler(bridgeClient),
            new TeleportToAetheryteToolHandler(bridgeClient),
            new AddonCallbackIntToolHandler(bridgeClient),
            new AddonCallbackValuesToolHandler(bridgeClient),
        ];

    private async Task TryRecordDeniedAsync(string eventType, string summary, CancellationToken cancellationToken)
    {
        if (auditRecorder is null)
        {
            return;
        }

        try
        {
            await auditRecorder(eventType, summary, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
