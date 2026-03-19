using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Host.Tests;

public sealed class McpRegistriesTests
{
    [Fact]
    public void ToolRegistry_CreateDefault_ContainsKnownObservationTools()
    {
        var registry = McpToolRegistry.CreateDefault();

        Assert.Contains(registry.Tools, static tool => tool.ToolName == "get_session_status");
        Assert.Contains(registry.Tools, static tool => tool.ToolName == "get_player_context");
        Assert.Contains(registry.Tools, static tool => tool.ToolName == "get_addon_tree");
    }

    [Fact]
    public void ResourceRegistry_CreateDefault_ContainsKnownObservationResources()
    {
        var registry = McpResourceRegistry.CreateDefault();

        Assert.Contains(registry.Resources, static resource => resource.UriTemplate == "ffxiv://session/status");
        Assert.Contains(registry.Resources, static resource => resource.UriTemplate == "ffxiv://player/context");
        Assert.Contains(registry.Resources, static resource => resource.UriTemplate == "ffxiv://ui/addons");
        Assert.Contains(registry.Resources, static resource => resource.UriTemplate == "ffxiv://ui/addon/{addonName}/tree");
        Assert.All(registry.Resources, static resource => Assert.Equal(McpContentTypes.ApplicationJson, resource.MimeType));
    }

    [Fact]
    public void Registries_ExcludeDeniedCapabilities()
    {
        var deniedCapability = new CapabilityDefinition(
            new CapabilityId("system.blocked"),
            "Blocked",
            "Denied capability.",
            CapabilityCategory.System,
            SensitivityLevel.Blocked,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: true,
            supportsTool: true,
            supportsResource: true,
            version: "1.0.0");
        var capabilityRegistry = new CapabilityRegistry(
            [deniedCapability],
            [new ToolBinding(deniedCapability.Id, "blocked_tool", "in", "out", "Handler", false)],
            [new ResourceBinding(deniedCapability.Id, "ffxiv://blocked/resource", "application/json", "Provider", false)],
            []);

        var toolRegistry = new McpToolRegistry(capabilityRegistry);
        var resourceRegistry = new McpResourceRegistry(capabilityRegistry);

        Assert.Empty(toolRegistry.Tools);
        Assert.Empty(resourceRegistry.Resources);
        Assert.True(toolRegistry.IsDenied("blocked_tool"));
        Assert.True(resourceRegistry.IsDenied("ffxiv://blocked/resource"));
    }

    [Fact]
    public void Registries_TrackDeniedAddons()
    {
        var capabilityRegistry = new CapabilityRegistry(
            [],
            [],
            [],
            [
                new AddonMetadata(
                    "BlockedAddon",
                    "Blocked Addon",
                    CapabilityCategory.Ui,
                    SensitivityLevel.Blocked,
                    DefaultEnabled: false,
                    Denied: true,
                    Recommended: false,
                    Notes: "Denied addon.",
                    IntrospectionModes: ["tree"],
                    ProfileAvailability: [ProfileType.Observation]),
            ]);

        var toolRegistry = new McpToolRegistry(capabilityRegistry);
        var resourceRegistry = new McpResourceRegistry(capabilityRegistry);

        Assert.True(toolRegistry.IsDeniedAddon("BlockedAddon"));
        Assert.True(resourceRegistry.IsDeniedAddon("BlockedAddon"));
    }

    [Fact]
    public void RegistryConsistencyValidator_ValidatesDefaultHostBindings()
    {
        var validator = new McpRegistryConsistencyValidator();

        validator.Validate(
            McpToolRegistry.CreateDefault(),
            McpResourceRegistry.CreateDefault(),
            new McpSchemaRegistry());
    }

    [Fact]
    public void RegistryConsistencyValidator_RejectsUnknownHandlerType()
    {
        var capability = new CapabilityDefinition(
            new CapabilityId("player.context"),
            "Player Context",
            "Read player context.",
            CapabilityCategory.Player,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: false,
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");
        var capabilityRegistry = new CapabilityRegistry(
            [capability],
            [new ToolBinding(capability.Id, "get_player_context", "playerContext.input", "playerContext.output", "MissingToolHandler", false)],
            [],
            []);
        var validator = new McpRegistryConsistencyValidator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            validator.Validate(
                new McpToolRegistry(capabilityRegistry),
                new McpResourceRegistry(capabilityRegistry),
                new McpSchemaRegistry()));

        Assert.Contains("MissingToolHandler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryConsistencyValidator_RejectsUnknownProviderType()
    {
        var capability = new CapabilityDefinition(
            new CapabilityId("player.context"),
            "Player Context",
            "Read player context.",
            CapabilityCategory.Player,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: false,
            supportsTool: false,
            supportsResource: true,
            version: "1.0.0");
        var capabilityRegistry = new CapabilityRegistry(
            [capability],
            [],
            [new ResourceBinding(capability.Id, "ffxiv://player/context", McpContentTypes.ApplicationJson, "MissingProvider", false)],
            []);
        var validator = new McpRegistryConsistencyValidator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            validator.Validate(
                new McpToolRegistry(capabilityRegistry),
                new McpResourceRegistry(capabilityRegistry),
                new McpSchemaRegistry()));

        Assert.Contains("MissingProvider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryConsistencyValidator_RejectsUnknownSchemaId()
    {
        var capability = new CapabilityDefinition(
            new CapabilityId("player.context"),
            "Player Context",
            "Read player context.",
            CapabilityCategory.Player,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: false,
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");
        var capabilityRegistry = new CapabilityRegistry(
            [capability],
            [new ToolBinding(capability.Id, "get_player_context", "missing.input", "playerContext.output", "PlayerContextToolHandler", false)],
            [],
            []);
        var validator = new McpRegistryConsistencyValidator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            validator.Validate(
                new McpToolRegistry(capabilityRegistry),
                new McpResourceRegistry(capabilityRegistry),
                new McpSchemaRegistry()));

        Assert.Contains("missing.input", exception.Message, StringComparison.Ordinal);
    }
}
