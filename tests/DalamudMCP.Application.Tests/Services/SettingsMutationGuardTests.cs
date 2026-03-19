using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.Services;

public sealed class SettingsMutationGuardTests
{
    [Fact]
    public void EnsureCanEnableTool_AllowsKnownNonDeniedTool()
    {
        var guard = new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault());

        guard.EnsureCanEnableTool("get_player_context");
    }

    [Fact]
    public void EnsureCanEnableAddon_ThrowsForDeniedAddon()
    {
        var registry = new CapabilityRegistry(
            [],
            [],
            [],
            [
                new AddonMetadata(
                    "ChatLog",
                    "Chat Log",
                    CapabilityCategory.System,
                    SensitivityLevel.Blocked,
                    DefaultEnabled: false,
                    Denied: true,
                    Recommended: false,
                    Notes: "Denied addon for testing.",
                    IntrospectionModes: ["tree"],
                    ProfileAvailability: [ProfileType.Observation]),
            ]);
        var guard = new SettingsMutationGuard(registry);

        var action = () => guard.EnsureCanEnableAddon("ChatLog");

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("denied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsurePolicyAllowed_ThrowsForUnknownResource()
    {
        var guard = new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault());
        var policy = new ExposurePolicy(enabledResources: ["ffxiv://unknown/resource"]);

        var action = () => guard.EnsurePolicyAllowed(policy);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("Unknown resource", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
