using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Tests.Capabilities;

public sealed class CapabilityDefinitionTests
{
    [Fact]
    public void ActionCapability_CannotExposeResources()
    {
        var action = () => new CapabilityDefinition(
            new CapabilityId("world.move"),
            "Move",
            "Move to a world point.",
            CapabilityCategory.Action,
            SensitivityLevel.High,
            ProfileType.Action,
            defaultEnabled: false,
            requiresConsent: true,
            denied: false,
            supportsTool: true,
            supportsResource: true,
            version: "1.0.0");

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Contains("cannot expose resources", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeniedCapability_MustBeBlocked()
    {
        var action = () => new CapabilityDefinition(
            new CapabilityId("chat.messages"),
            "Chat Messages",
            "Blocked capability.",
            CapabilityCategory.System,
            SensitivityLevel.High,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: true,
            denied: true,
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Contains("blocked sensitivity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
