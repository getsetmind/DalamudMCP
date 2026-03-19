using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Host.Resources;

namespace DalamudMCP.Host;

public sealed class McpResourceReader
{
    private readonly Func<string, string, CancellationToken, Task>? auditRecorder;
    private readonly Func<CancellationToken, Task<CapabilityStateResponse>>? capabilityStateProvider;
    private readonly IReadOnlyList<IMcpResourceProvider> providers;
    private readonly McpResourceRegistry resourceRegistry;

    public McpResourceReader(PluginBridgeClient bridgeClient, McpResourceRegistry resourceRegistry)
        : this(CreateDefaultProviders(bridgeClient), resourceRegistry, bridgeClient.GetCapabilityStateAsync, bridgeClient.RecordAuditEventAsync)
    {
    }

    public McpResourceReader(
        IEnumerable<IMcpResourceProvider> providers,
        McpResourceRegistry resourceRegistry,
        Func<CancellationToken, Task<CapabilityStateResponse>>? capabilityStateProvider,
        Func<string, string, CancellationToken, Task>? auditRecorder = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        this.resourceRegistry = resourceRegistry;
        this.capabilityStateProvider = capabilityStateProvider;
        this.auditRecorder = auditRecorder;
        this.providers = providers.ToArray();
    }

    public async Task<object> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (!McpResourceUri.TryNormalize(uri, out var normalizedUri))
        {
            throw new InvalidOperationException($"Unsupported resource uri '{uri}'.");
        }

        if (resourceRegistry.IsDenied(normalizedUri)
            || (TryMatchAddonUri(normalizedUri, "ffxiv://ui/addon/", "/tree", out _)
                && resourceRegistry.IsDenied("ffxiv://ui/addon/{addonName}/tree"))
            || (TryMatchAddonUri(normalizedUri, "ffxiv://ui/addon/", "/strings", out _)
                && resourceRegistry.IsDenied("ffxiv://ui/addon/{addonName}/strings")))
        {
            await TryRecordDeniedAsync("resource.request_denied", $"resource={normalizedUri}; reason=capability_denied", cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Resource '{normalizedUri}' is denied.");
        }

        var capabilityState = await GetCapabilityStateAsync(cancellationToken).ConfigureAwait(false);
        if (!IsResourceEnabled(normalizedUri, capabilityState))
        {
            throw new InvalidOperationException($"Resource '{normalizedUri}' is not enabled.");
        }

        if (!IsAddonAllowed(normalizedUri, capabilityState))
        {
            await TryRecordDeniedAsync("resource.request_denied", $"resource={normalizedUri}; reason=addon_denied_or_disabled", cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Addon access for resource '{normalizedUri}' is not enabled.");
        }

        var provider = providers.FirstOrDefault(candidate => candidate.CanHandle(normalizedUri));
        if (provider is null)
        {
            throw new InvalidOperationException($"Unsupported resource uri '{normalizedUri}'.");
        }

        return await provider.ReadAsync(normalizedUri, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CapabilityStateResponse?> GetCapabilityStateAsync(CancellationToken cancellationToken)
    {
        if (capabilityStateProvider is null)
        {
            return null;
        }

        return await capabilityStateProvider(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsResourceEnabled(string uri, CapabilityStateResponse? capabilityState)
    {
        if (capabilityState is null)
        {
            return true;
        }

        if (!capabilityState.ObservationProfileEnabled)
        {
            return false;
        }

        return capabilityState.EnabledResources.Contains(uri, StringComparer.OrdinalIgnoreCase)
            || (TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/tree", out _)
                && capabilityState.EnabledResources.Contains("ffxiv://ui/addon/{addonName}/tree", StringComparer.OrdinalIgnoreCase))
            || (TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/strings", out _)
                && capabilityState.EnabledResources.Contains("ffxiv://ui/addon/{addonName}/strings", StringComparer.OrdinalIgnoreCase));
    }

    private bool IsAddonAllowed(string uri, CapabilityStateResponse? capabilityState)
    {
        if (capabilityState is null)
        {
            return true;
        }

        if (TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/tree", out var treeAddonName))
        {
            if (resourceRegistry.IsDeniedAddon(treeAddonName))
            {
                return false;
            }

            return capabilityState.EnabledAddons.Contains(treeAddonName, StringComparer.OrdinalIgnoreCase);
        }

        if (TryMatchAddonUri(uri, "ffxiv://ui/addon/", "/strings", out var stringsAddonName))
        {
            if (resourceRegistry.IsDeniedAddon(stringsAddonName))
            {
                return false;
            }

            return capabilityState.EnabledAddons.Contains(stringsAddonName, StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool TryMatchAddonUri(string uri, string prefix, string suffix, out string addonName)
    {
        addonName = string.Empty;
        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !uri.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = uri[prefix.Length..^suffix.Length];
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        addonName = candidate;
        return true;
    }

    private static IReadOnlyList<IMcpResourceProvider> CreateDefaultProviders(PluginBridgeClient bridgeClient) =>
        [
            new SessionStatusResourceProvider(bridgeClient),
            new PlayerContextResourceProvider(bridgeClient),
            new DutyContextResourceProvider(bridgeClient),
            new InventorySummaryResourceProvider(bridgeClient),
            new AddonCatalogResourceProvider(bridgeClient),
            new AddonTreeResourceProvider(bridgeClient),
            new AddonStringsResourceProvider(bridgeClient),
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
