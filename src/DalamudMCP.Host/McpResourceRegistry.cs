using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Host;

public sealed class McpResourceRegistry
{
    private readonly HashSet<string> deniedAddonNames;
    private readonly HashSet<string> deniedResourceTemplates;
    private readonly Dictionary<string, McpResourceDefinition> resourcesByTemplate;

    public McpResourceRegistry(CapabilityRegistry capabilityRegistry)
    {
        ArgumentNullException.ThrowIfNull(capabilityRegistry);

        Resources = capabilityRegistry.ResourceBindings
            .Join(
                capabilityRegistry.Capabilities.Where(static capability => !capability.Denied),
                static binding => binding.CapabilityId.Value,
                static capability => capability.Id.Value,
                static (binding, capability) => new McpResourceDefinition(
                    binding.UriTemplate,
                    binding.CapabilityId.Value,
                    binding.MimeType,
                    binding.ProviderType,
                    binding.SupportsSubscription,
                    capability.DisplayName,
                    capability.Description))
            .OrderBy(static resource => resource.UriTemplate, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        deniedResourceTemplates = capabilityRegistry.ResourceBindings
            .Where(binding => capabilityRegistry.IsDeniedResource(binding.UriTemplate))
            .Select(static binding => binding.UriTemplate)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        deniedAddonNames = capabilityRegistry.Addons
            .Where(static addon => addon.Denied)
            .Select(static addon => addon.AddonName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        resourcesByTemplate = Resources.ToDictionary(static resource => resource.UriTemplate, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<McpResourceDefinition> Resources { get; }

    public static McpResourceRegistry CreateDefault() =>
        new(KnownCapabilityRegistry.CreateDefault());

    public bool TryGet(string uriTemplate, out McpResourceDefinition? resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        return resourcesByTemplate.TryGetValue(uriTemplate, out resource);
    }

    public bool IsDenied(string uriTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        return deniedResourceTemplates.Contains(uriTemplate);
    }

    public bool IsDeniedAddon(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        return deniedAddonNames.Contains(addonName);
    }
}
