using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Registry;

public static class KnownCapabilityRegistry
{
    public static CapabilityRegistry CreateDefault() =>
        new(
            CreateCapabilities(),
            CreateToolBindings(),
            CreateResourceBindings(),
            CreateAddonMetadata());

    private static IReadOnlyList<CapabilityDefinition> CreateCapabilities() =>
        [
            new CapabilityDefinition(
                new CapabilityId("session.status"),
                "Session Status",
                "Read plugin bridge and reader readiness state for diagnostics.",
                CapabilityCategory.System,
                SensitivityLevel.Low,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: false,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "session",
                "status",
                "diagnostics"),
            new CapabilityDefinition(
                new CapabilityId("player.context"),
                "Player Context",
                "Read coarse self-player context for MCP tools and resources.",
                CapabilityCategory.Player,
                SensitivityLevel.Low,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: false,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "player",
                "context"),
            new CapabilityDefinition(
                new CapabilityId("duty.context"),
                "Duty Context",
                "Read current duty and territory context.",
                CapabilityCategory.Duty,
                SensitivityLevel.Low,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: false,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "duty",
                "context"),
            new CapabilityDefinition(
                new CapabilityId("inventory.summary"),
                "Inventory Summary",
                "Read inventory summary data without exposing raw item-by-item detail.",
                CapabilityCategory.Inventory,
                SensitivityLevel.Medium,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: true,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "inventory",
                "summary"),
            new CapabilityDefinition(
                new CapabilityId("ui.addonCatalog"),
                "Addon Catalog",
                "List observable addons that are currently known to the plugin.",
                CapabilityCategory.Ui,
                SensitivityLevel.Medium,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: true,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "ui",
                "addons"),
            new CapabilityDefinition(
                new CapabilityId("ui.addonTree"),
                "Addon Tree",
                "Read structural node information from allowlisted addons.",
                CapabilityCategory.Ui,
                SensitivityLevel.Medium,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: true,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "ui",
                "tree"),
            new CapabilityDefinition(
                new CapabilityId("ui.stringTable"),
                "String Table",
                "Read decoded string table content from allowlisted addons.",
                CapabilityCategory.Ui,
                SensitivityLevel.Medium,
                ProfileType.Observation,
                defaultEnabled: false,
                requiresConsent: true,
                denied: false,
                supportsTool: true,
                supportsResource: true,
                version: "1.0.0",
                "ui",
                "strings"),
        ];

    private static IReadOnlyList<ToolBinding> CreateToolBindings() =>
        [
            new ToolBinding(new CapabilityId("session.status"), "get_session_status", "sessionStatus.input", "sessionStatus.output", "SessionStatusToolHandler", false),
            new ToolBinding(new CapabilityId("player.context"), "get_player_context", "playerContext.input", "playerContext.output", "PlayerContextToolHandler", false),
            new ToolBinding(new CapabilityId("duty.context"), "get_duty_context", "dutyContext.input", "dutyContext.output", "DutyContextToolHandler", false),
            new ToolBinding(new CapabilityId("inventory.summary"), "get_inventory_summary", "inventorySummary.input", "inventorySummary.output", "InventorySummaryToolHandler", false),
            new ToolBinding(new CapabilityId("ui.addonCatalog"), "get_addon_list", "addonCatalog.input", "addonCatalog.output", "AddonListToolHandler", false),
            new ToolBinding(new CapabilityId("ui.addonTree"), "get_addon_tree", "addonTree.input", "addonTree.output", "AddonTreeToolHandler", false),
            new ToolBinding(new CapabilityId("ui.stringTable"), "get_addon_strings", "stringTable.input", "stringTable.output", "AddonStringsToolHandler", false),
        ];

    private static IReadOnlyList<ResourceBinding> CreateResourceBindings() =>
        [
            new ResourceBinding(new CapabilityId("session.status"), "ffxiv://session/status", McpContentTypes.ApplicationJson, "SessionStatusResourceProvider", false),
            new ResourceBinding(new CapabilityId("player.context"), "ffxiv://player/context", McpContentTypes.ApplicationJson, "PlayerContextResourceProvider", false),
            new ResourceBinding(new CapabilityId("duty.context"), "ffxiv://duty/context", McpContentTypes.ApplicationJson, "DutyContextResourceProvider", false),
            new ResourceBinding(new CapabilityId("inventory.summary"), "ffxiv://inventory/summary", McpContentTypes.ApplicationJson, "InventorySummaryResourceProvider", false),
            new ResourceBinding(new CapabilityId("ui.addonCatalog"), "ffxiv://ui/addons", McpContentTypes.ApplicationJson, "AddonCatalogResourceProvider", false),
            new ResourceBinding(new CapabilityId("ui.addonTree"), "ffxiv://ui/addon/{addonName}/tree", McpContentTypes.ApplicationJson, "AddonTreeResourceProvider", false),
            new ResourceBinding(new CapabilityId("ui.stringTable"), "ffxiv://ui/addon/{addonName}/strings", McpContentTypes.ApplicationJson, "AddonStringsResourceProvider", false),
        ];

    private static IReadOnlyList<AddonMetadata> CreateAddonMetadata() =>
        [
            new AddonMetadata(
                "Inventory",
                "Inventory",
                CapabilityCategory.Ui,
                SensitivityLevel.Low,
                DefaultEnabled: false,
                Denied: false,
                Recommended: true,
                Notes: "Safe baseline inventory summary and UI tree inspection target.",
                IntrospectionModes: ["tree", "strings"],
                ProfileAvailability: [ProfileType.Observation]),
            new AddonMetadata(
                "Character",
                "Character",
                CapabilityCategory.Ui,
                SensitivityLevel.Low,
                DefaultEnabled: false,
                Denied: false,
                Recommended: true,
                Notes: "Character sheet addon for baseline UI inspection.",
                IntrospectionModes: ["tree", "strings"],
                ProfileAvailability: [ProfileType.Observation]),
            new AddonMetadata(
                "ContentsInfoDetail",
                "Duty Detail",
                CapabilityCategory.Ui,
                SensitivityLevel.Low,
                DefaultEnabled: false,
                Denied: false,
                Recommended: false,
                Notes: "Duty detail panes can be useful for exploratory UI introspection.",
                IntrospectionModes: ["tree", "strings"],
                ProfileAvailability: [ProfileType.Observation]),
        ];
}
