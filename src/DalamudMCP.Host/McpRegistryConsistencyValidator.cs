using DalamudMCP.Host.Resources;
using DalamudMCP.Host.Tools;

namespace DalamudMCP.Host;

public sealed class McpRegistryConsistencyValidator
{
    private readonly HashSet<string> providerTypeNames;
    private readonly HashSet<string> toolHandlerTypeNames;

    public McpRegistryConsistencyValidator()
        : this(CreateDefaultToolHandlerTypeNames(), CreateDefaultProviderTypeNames())
    {
    }

    public McpRegistryConsistencyValidator(IEnumerable<string> toolHandlerTypeNames, IEnumerable<string> providerTypeNames)
    {
        ArgumentNullException.ThrowIfNull(toolHandlerTypeNames);
        ArgumentNullException.ThrowIfNull(providerTypeNames);
        this.toolHandlerTypeNames = toolHandlerTypeNames.ToHashSet(StringComparer.Ordinal);
        this.providerTypeNames = providerTypeNames.ToHashSet(StringComparer.Ordinal);
    }

    public void Validate(McpToolRegistry toolRegistry, McpResourceRegistry resourceRegistry, McpSchemaRegistry schemaRegistry)
    {
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(resourceRegistry);
        ArgumentNullException.ThrowIfNull(schemaRegistry);

        foreach (var tool in toolRegistry.Tools)
        {
            schemaRegistry.GetRequired(tool.InputSchemaId);
            schemaRegistry.GetRequired(tool.OutputSchemaId);

            if (!toolHandlerTypeNames.Contains(tool.HandlerType))
            {
                throw new InvalidOperationException(
                    $"Tool '{tool.ToolName}' references unknown handler type '{tool.HandlerType}'.");
            }
        }

        foreach (var resource in resourceRegistry.Resources)
        {
            if (!providerTypeNames.Contains(resource.ProviderType))
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.UriTemplate}' references unknown provider type '{resource.ProviderType}'.");
            }
        }
    }

    private static IReadOnlyCollection<string> CreateDefaultToolHandlerTypeNames() =>
        [
            nameof(SessionStatusToolHandler),
            nameof(PlayerContextToolHandler),
            nameof(DutyContextToolHandler),
            nameof(InventorySummaryToolHandler),
            nameof(AddonListToolHandler),
            nameof(AddonTreeToolHandler),
            nameof(AddonStringsToolHandler),
            nameof(NearbyInteractablesToolHandler),
            nameof(TargetObjectToolHandler),
            nameof(InteractWithTargetToolHandler),
            nameof(MoveToEntityToolHandler),
            nameof(TeleportToAetheryteToolHandler),
            nameof(AddonCallbackIntToolHandler),
            nameof(AddonCallbackValuesToolHandler),
        ];

    private static IReadOnlyCollection<string> CreateDefaultProviderTypeNames() =>
        [
            nameof(SessionStatusResourceProvider),
            nameof(PlayerContextResourceProvider),
            nameof(DutyContextResourceProvider),
            nameof(InventorySummaryResourceProvider),
            nameof(AddonCatalogResourceProvider),
            nameof(AddonTreeResourceProvider),
            nameof(AddonStringsResourceProvider),
        ];
}
