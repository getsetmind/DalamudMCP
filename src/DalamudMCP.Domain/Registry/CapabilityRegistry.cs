using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Registry;

public sealed class CapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDefinition> capabilitiesById;
    private readonly Dictionary<string, ToolBinding> toolBindingsByName;
    private readonly Dictionary<string, ResourceBinding> resourceBindingsByTemplate;
    private readonly Dictionary<string, AddonMetadata> addonsByName;
    private readonly HashSet<string> deniedToolNames;
    private readonly HashSet<string> deniedResourceTemplates;
    private readonly HashSet<string> deniedAddonNames;

    public CapabilityRegistry(
        IEnumerable<CapabilityDefinition> capabilities,
        IEnumerable<ToolBinding> toolBindings,
        IEnumerable<ResourceBinding> resourceBindings,
        IEnumerable<AddonMetadata> addons)
    {
        Capabilities = capabilities.ToArray();
        ToolBindings = toolBindings.ToArray();
        ResourceBindings = resourceBindings.ToArray();
        Addons = addons.ToArray();
        Validate();

        capabilitiesById = Capabilities.ToDictionary(static capability => capability.Id.Value, StringComparer.OrdinalIgnoreCase);
        toolBindingsByName = ToolBindings.ToDictionary(static binding => binding.ToolName, StringComparer.OrdinalIgnoreCase);
        resourceBindingsByTemplate = ResourceBindings.ToDictionary(static binding => binding.UriTemplate, StringComparer.OrdinalIgnoreCase);
        addonsByName = Addons.ToDictionary(static addon => addon.AddonName, StringComparer.OrdinalIgnoreCase);
        deniedToolNames = BuildDeniedToolNames();
        deniedResourceTemplates = BuildDeniedResourceTemplates();
        deniedAddonNames = Addons
            .Where(static addon => addon.Denied)
            .Select(static addon => addon.AddonName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CapabilityDefinition> Capabilities { get; }

    public IReadOnlyList<ToolBinding> ToolBindings { get; }

    public IReadOnlyList<ResourceBinding> ResourceBindings { get; }

    public IReadOnlyList<AddonMetadata> Addons { get; }

    public bool TryGetCapability(string capabilityId, out CapabilityDefinition? capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        return capabilitiesById.TryGetValue(capabilityId, out capability);
    }

    public bool TryGetToolBinding(string toolName, out ToolBinding? binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolBindingsByName.TryGetValue(toolName, out binding);
    }

    public bool TryGetResourceBinding(string uriTemplate, out ResourceBinding? binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        return resourceBindingsByTemplate.TryGetValue(uriTemplate, out binding);
    }

    public bool TryGetAddon(string addonName, out AddonMetadata? addon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        return addonsByName.TryGetValue(addonName, out addon);
    }

    public bool IsDeniedTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return deniedToolNames.Contains(toolName);
    }

    public bool IsDeniedResource(string uriTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        return deniedResourceTemplates.Contains(uriTemplate);
    }

    public bool IsDeniedAddon(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        return deniedAddonNames.Contains(addonName);
    }

    private void Validate()
    {
        EnsureUniqueCapabilities();
        EnsureUniqueToolNames();
        EnsureUniqueResourceTemplates();
        EnsureUniqueAddonNames();
        EnsureBindingsReferenceKnownCapabilities();
        EnsureBindingsMatchCapabilitySurfaces();
        EnsureDeniedDefaultsAreDisabled();
        EnsureBlockedAddonsAreDisabled();
    }

    private void EnsureUniqueCapabilities()
    {
        var duplicates = Capabilities
            .GroupBy(static capability => capability.Id.Value, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate capability ids found: {string.Join(", ", duplicates)}");
        }
    }

    private void EnsureUniqueToolNames()
    {
        var duplicates = ToolBindings
            .GroupBy(static binding => binding.ToolName, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate tool names found: {string.Join(", ", duplicates)}");
        }
    }

    private void EnsureUniqueResourceTemplates()
    {
        var duplicates = ResourceBindings
            .GroupBy(static binding => binding.UriTemplate, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate resource templates found: {string.Join(", ", duplicates)}");
        }
    }

    private void EnsureUniqueAddonNames()
    {
        var duplicates = Addons
            .GroupBy(static addon => addon.AddonName, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate addon names found: {string.Join(", ", duplicates)}");
        }
    }

    private void EnsureBindingsReferenceKnownCapabilities()
    {
        var capabilityIds = Capabilities
            .Select(static capability => capability.Id.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in ToolBindings)
        {
            if (!capabilityIds.Contains(binding.CapabilityId.Value))
            {
                throw new InvalidOperationException($"Tool binding references unknown capability '{binding.CapabilityId}'.");
            }
        }

        foreach (var binding in ResourceBindings)
        {
            if (!capabilityIds.Contains(binding.CapabilityId.Value))
            {
                throw new InvalidOperationException($"Resource binding references unknown capability '{binding.CapabilityId}'.");
            }
        }
    }

    private void EnsureBindingsMatchCapabilitySurfaces()
    {
        foreach (var binding in ToolBindings)
        {
            var capability = Capabilities.Single(capability => capability.Id.Value == binding.CapabilityId.Value);
            if (!capability.SupportsTool)
            {
                throw new InvalidOperationException($"Tool binding '{binding.ToolName}' targets capability '{capability.Id.Value}' that does not support tools.");
            }
        }

        foreach (var binding in ResourceBindings)
        {
            var capability = Capabilities.Single(capability => capability.Id.Value == binding.CapabilityId.Value);
            if (!capability.SupportsResource)
            {
                throw new InvalidOperationException($"Resource binding '{binding.UriTemplate}' targets capability '{capability.Id.Value}' that does not support resources.");
            }
        }
    }

    private void EnsureDeniedDefaultsAreDisabled()
    {
        var invalid = Capabilities
            .Where(static capability => capability.Denied && capability.DefaultEnabled)
            .Select(static capability => capability.Id.Value)
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new InvalidOperationException($"Denied capabilities cannot be enabled by default: {string.Join(", ", invalid)}");
        }
    }

    private void EnsureBlockedAddonsAreDisabled()
    {
        var invalid = Addons
            .Where(static addon => addon.Denied && addon.DefaultEnabled)
            .Select(static addon => addon.AddonName)
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new InvalidOperationException($"Denied addons cannot be enabled by default: {string.Join(", ", invalid)}");
        }
    }

    private HashSet<string> BuildDeniedToolNames()
    {
        var deniedCapabilities = Capabilities
            .Where(static capability => capability.Denied)
            .Select(static capability => capability.Id.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ToolBindings
            .Where(binding => deniedCapabilities.Contains(binding.CapabilityId.Value))
            .Select(static binding => binding.ToolName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private HashSet<string> BuildDeniedResourceTemplates()
    {
        var deniedCapabilities = Capabilities
            .Where(static capability => capability.Denied)
            .Select(static capability => capability.Id.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ResourceBindings
            .Where(binding => deniedCapabilities.Contains(binding.CapabilityId.Value))
            .Select(static binding => binding.UriTemplate)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
