using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Domain.Tests.Registry;

public sealed class CapabilityRegistryTests
{
    [Fact]
    public void Constructor_Throws_WhenToolNamesAreDuplicated()
    {
        var capability = new CapabilityDefinition(
            new CapabilityId("player.context"),
            "Player Context",
            "Read player context.",
            CapabilityCategory.Player,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: true,
            requiresConsent: false,
            denied: false,
            supportsTool: true,
            supportsResource: true,
            version: "1.0.0");

        var action = () => new CapabilityRegistry(
            [capability],
            [
                new ToolBinding(capability.Id, "get_player_context", "input", "output", "HandlerA", false),
                new ToolBinding(capability.Id, "get_player_context", "input", "output", "HandlerB", false),
            ],
            [],
            []);

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Contains("Duplicate tool names", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_Throws_WhenResourceTemplatesAreDuplicated()
    {
        var capability = CreateObservationCapability();

        var action = () => new CapabilityRegistry(
            [capability],
            [],
            [
                new ResourceBinding(capability.Id, "ffxiv://player/context", "application/json", "ProviderA", false),
                new ResourceBinding(capability.Id, "ffxiv://player/context", "application/json", "ProviderB", false),
            ],
            []);

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Contains("Duplicate resource templates", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_Throws_WhenAddonNamesAreDuplicated()
    {
        var action = () => new CapabilityRegistry(
            [],
            [],
            [],
            [
                new AddonMetadata("Inventory", "Inventory", CapabilityCategory.Ui, SensitivityLevel.Low, false, false, true, "A", ["tree"], [ProfileType.Observation]),
                new AddonMetadata("Inventory", "Inventory 2", CapabilityCategory.Ui, SensitivityLevel.Low, false, false, false, "B", ["strings"], [ProfileType.Observation]),
            ]);

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Contains("Duplicate addon names", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_Throws_WhenToolBindingTargetsCapabilityWithoutToolSupport()
    {
        var capability = new CapabilityDefinition(
            new CapabilityId("player.context"),
            "Player Context",
            "Read player context.",
            CapabilityCategory.Player,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: true,
            requiresConsent: false,
            denied: false,
            supportsTool: false,
            supportsResource: true,
            version: "1.0.0");

        var action = () => new CapabilityRegistry(
            [capability],
            [new ToolBinding(capability.Id, "get_player_context", "input", "output", "HandlerA", false)],
            [],
            []);

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Contains("does not support tools", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetMembers_ReturnsRegisteredEntries()
    {
        var registry = KnownCapabilityRegistry.CreateDefault();

        Assert.True(registry.TryGetCapability("player.context", out var capability));
        Assert.True(registry.TryGetToolBinding("get_player_context", out var toolBinding));
        Assert.True(registry.TryGetResourceBinding("ffxiv://player/context", out var resourceBinding));
        Assert.True(registry.TryGetAddon("Inventory", out var addon));
        Assert.Equal("player.context", capability!.Id.Value);
        Assert.Equal("get_player_context", toolBinding!.ToolName);
        Assert.Equal("ffxiv://player/context", resourceBinding!.UriTemplate);
        Assert.Equal("Inventory", addon!.AddonName);
    }

    [Fact]
    public void IsDeniedHelpers_ReturnDeniedSurfaces()
    {
        var deniedCapability = new CapabilityDefinition(
            new CapabilityId("system.denied"),
            "Denied",
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
        var registry = new CapabilityRegistry(
            [deniedCapability],
            [new ToolBinding(deniedCapability.Id, "blocked_tool", "in", "out", "Handler", false)],
            [new ResourceBinding(deniedCapability.Id, "ffxiv://blocked/resource", "application/json", "Provider", false)],
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

        Assert.True(registry.IsDeniedTool("blocked_tool"));
        Assert.True(registry.IsDeniedResource("ffxiv://blocked/resource"));
        Assert.True(registry.IsDeniedAddon("BlockedAddon"));
    }

    private static CapabilityDefinition CreateObservationCapability() =>
        new(
            new CapabilityId("player.context"),
            "Player Context",
            "Read player context.",
            CapabilityCategory.Player,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: true,
            requiresConsent: false,
            denied: false,
            supportsTool: true,
            supportsResource: true,
            version: "1.0.0");
}
