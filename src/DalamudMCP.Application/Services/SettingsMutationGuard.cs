using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Services;

public sealed class SettingsMutationGuard
{
    private readonly CapabilityRegistry capabilityRegistry;

    public SettingsMutationGuard(CapabilityRegistry capabilityRegistry)
    {
        this.capabilityRegistry = capabilityRegistry;
    }

    public void EnsureCanEnableTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!capabilityRegistry.TryGetToolBinding(toolName, out var binding) || binding is null)
        {
            throw new InvalidOperationException($"Unknown tool '{toolName}'.");
        }

        var capability = GetCapability(binding.CapabilityId.Value);
        if (capability.Denied)
        {
            throw new InvalidOperationException($"Tool '{toolName}' is denied by the capability registry.");
        }
    }

    public void EnsureCanEnableResource(string uriTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);

        if (!capabilityRegistry.TryGetResourceBinding(uriTemplate, out var binding) || binding is null)
        {
            throw new InvalidOperationException($"Unknown resource '{uriTemplate}'.");
        }

        var capability = GetCapability(binding.CapabilityId.Value);
        if (capability.Denied)
        {
            throw new InvalidOperationException($"Resource '{uriTemplate}' is denied by the capability registry.");
        }
    }

    public void EnsureCanEnableAddon(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);

        if (!capabilityRegistry.TryGetAddon(addonName, out var addon) || addon is null)
        {
            throw new InvalidOperationException($"Unknown addon '{addonName}'.");
        }

        if (addon.Denied)
        {
            throw new InvalidOperationException($"Addon '{addonName}' is denied by the capability registry.");
        }
    }

    public void EnsurePolicyAllowed(ExposurePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        foreach (var toolName in policy.EnabledTools)
        {
            EnsureCanEnableTool(toolName);
        }

        foreach (var uriTemplate in policy.EnabledResources)
        {
            EnsureCanEnableResource(uriTemplate);
        }

        foreach (var addonName in policy.EnabledAddons)
        {
            EnsureCanEnableAddon(addonName);
        }
    }

    private Domain.Capabilities.CapabilityDefinition GetCapability(string capabilityId)
    {
        if (!capabilityRegistry.TryGetCapability(capabilityId, out var capability) || capability is null)
        {
            throw new InvalidOperationException($"Unknown capability '{capabilityId}'.");
        }

        return capability;
    }
}
