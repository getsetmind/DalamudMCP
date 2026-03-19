using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Domain.Tests.Registry;

public sealed class KnownCapabilityRegistryTests
{
    [Fact]
    public void CreateDefault_ReturnsExpectedObservationCapabilities()
    {
        var registry = KnownCapabilityRegistry.CreateDefault();

        Assert.Contains(registry.Capabilities, static capability => capability.Id.Value == "session.status");
        Assert.Contains(registry.Capabilities, static capability => capability.Id.Value == "player.context");
        Assert.Contains(registry.Capabilities, static capability => capability.Id.Value == "ui.addonTree");
        Assert.Contains(registry.Capabilities, static capability => capability.Id.Value == "world.nearbyInteractables");
        Assert.Contains(registry.Capabilities, static capability => capability.Id.Value == "world.targetObject");
        Assert.Contains(registry.Capabilities, static capability => capability.Id.Value == "world.teleportToAetheryte");
        Assert.Contains(registry.ToolBindings, static binding => binding.ToolName == "get_player_context");
        Assert.Contains(registry.ToolBindings, static binding => binding.ToolName == "get_session_status");
        Assert.Contains(registry.ToolBindings, static binding => binding.ToolName == "get_nearby_interactables");
        Assert.Contains(registry.ToolBindings, static binding => binding.ToolName == "target_object");
        Assert.Contains(registry.ToolBindings, static binding => binding.ToolName == "teleport_to_aetheryte");
        Assert.Contains(registry.ResourceBindings, static binding => binding.UriTemplate == "ffxiv://session/status");
        Assert.Contains(registry.ResourceBindings, static binding => binding.UriTemplate == "ffxiv://player/context");
        Assert.Contains(registry.ResourceBindings, static binding => binding.UriTemplate == "ffxiv://ui/addons");
    }

    [Fact]
    public void CreateDefault_ReturnsRecommendedInventoryAddon()
    {
        var registry = KnownCapabilityRegistry.CreateDefault();

        var addon = Assert.Single(registry.Addons, static addon => addon.AddonName == "Inventory");
        Assert.True(addon.Recommended);
        Assert.Contains("tree", addon.IntrospectionModes);
    }

    [Fact]
    public void CreateDefault_UsesApplicationJson_ForAllResourceBindings()
    {
        var registry = KnownCapabilityRegistry.CreateDefault();

        Assert.All(
            registry.ResourceBindings,
            static binding => Assert.Equal(McpContentTypes.ApplicationJson, binding.MimeType));
    }
}
