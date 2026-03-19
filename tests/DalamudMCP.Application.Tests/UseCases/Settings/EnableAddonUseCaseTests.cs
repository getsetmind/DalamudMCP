using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Settings;

public sealed class EnableAddonUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EnablesKnownAddon_AndWritesAudit()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var useCase = new EnableAddonUseCase(
            settings,
            audit,
            new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault()));

        await useCase.ExecuteAsync("Inventory", CancellationToken.None);

        Assert.Contains("Inventory", settings.Policy.EnabledAddons);
        Assert.Equal("addon.enabled", Assert.Single(audit.Events).EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForDeniedAddon_AndDoesNotWrite()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
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
                    Notes: "Denied for testing.",
                    IntrospectionModes: ["tree"],
                    ProfileAvailability: [ProfileType.Observation])
            ]);
        var useCase = new EnableAddonUseCase(settings, audit, new SettingsMutationGuard(registry));

        var action = () => useCase.ExecuteAsync("ChatLog", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Empty(settings.Policy.EnabledAddons);
        Assert.Empty(audit.Events);
    }
}
