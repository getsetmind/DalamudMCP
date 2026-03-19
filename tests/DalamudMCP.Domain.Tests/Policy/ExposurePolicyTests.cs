using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Domain.Tests.Policy;

public sealed class ExposurePolicyTests
{
    [Fact]
    public void CanExposeTool_ReturnsFalse_WhenActionProfileDisabled()
    {
        var capability = new CapabilityDefinition(
            new CapabilityId("world.interact"),
            "Interact With Target",
            "Interact with the current target.",
            CapabilityCategory.Action,
            SensitivityLevel.High,
            ProfileType.Action,
            defaultEnabled: false,
            requiresConsent: true,
            denied: false,
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");

        var policy = new ExposurePolicy(enabledTools: ["interact_with_target"]);

        var canExpose = policy.CanExposeTool(capability, "interact_with_target");

        Assert.False(canExpose);
    }

    [Fact]
    public void CanExposeResource_ReturnsTrue_WhenObservationResourceEnabled()
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

        var policy = new ExposurePolicy(enabledResources: ["ffxiv://player/context"]);

        var canExpose = policy.CanExposeResource(capability, "ffxiv://player/context");

        Assert.True(canExpose);
    }

    [Fact]
    public void ReplaceSelections_ReplacesAllEnabledCollections()
    {
        var policy = new ExposurePolicy(
            enabledTools: ["old_tool"],
            enabledResources: ["ffxiv://old/resource"],
            enabledAddons: ["OldAddon"]);

        var updated = policy.ReplaceSelections(
            ["new_tool"],
            ["ffxiv://player/context"],
            ["Inventory"]);

        Assert.Equal(["new_tool"], updated.EnabledTools.OrderBy(static value => value).ToArray());
        Assert.Equal(["ffxiv://player/context"], updated.EnabledResources.OrderBy(static value => value).ToArray());
        Assert.Equal(["Inventory"], updated.EnabledAddons.OrderBy(static value => value).ToArray());
    }

    [Fact]
    public void WithProfiles_UpdatesProfileFlags()
    {
        var policy = ExposurePolicy.Default;

        var updated = policy.WithProfiles(observationProfileEnabled: false, actionProfileEnabled: true);

        Assert.False(updated.ObservationProfileEnabled);
        Assert.True(updated.ActionProfileEnabled);
    }
}
