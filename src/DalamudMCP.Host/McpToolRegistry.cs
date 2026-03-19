using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Host;

public sealed class McpToolRegistry
{
    private readonly HashSet<string> deniedAddonNames;
    private readonly HashSet<string> deniedToolNames;
    private readonly Dictionary<string, McpToolDefinition> toolsByName;

    public McpToolRegistry(CapabilityRegistry capabilityRegistry)
    {
        ArgumentNullException.ThrowIfNull(capabilityRegistry);

        Tools = capabilityRegistry.ToolBindings
            .Join(
                capabilityRegistry.Capabilities.Where(static capability => !capability.Denied),
                static binding => binding.CapabilityId.Value,
                static capability => capability.Id.Value,
                static (binding, capability) => new McpToolDefinition(
                    binding.ToolName,
                    binding.CapabilityId.Value,
                    binding.InputSchemaId,
                    binding.OutputSchemaId,
                    binding.HandlerType,
                    binding.Experimental,
                    capability.DisplayName,
                    capability.Description))
            .OrderBy(static tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        deniedToolNames = capabilityRegistry.ToolBindings
            .Where(binding => capabilityRegistry.IsDeniedTool(binding.ToolName))
            .Select(static binding => binding.ToolName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        deniedAddonNames = capabilityRegistry.Addons
            .Where(static addon => addon.Denied)
            .Select(static addon => addon.AddonName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        toolsByName = Tools.ToDictionary(static tool => tool.ToolName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<McpToolDefinition> Tools { get; }

    public static McpToolRegistry CreateDefault() =>
        new(KnownCapabilityRegistry.CreateDefault());

    public bool TryGet(string toolName, out McpToolDefinition? tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolsByName.TryGetValue(toolName, out tool);
    }

    public bool IsDenied(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return deniedToolNames.Contains(toolName);
    }

    public bool IsDeniedAddon(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        return deniedAddonNames.Contains(addonName);
    }
}
