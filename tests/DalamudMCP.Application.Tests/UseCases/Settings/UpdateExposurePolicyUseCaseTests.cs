using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Settings;

public sealed class UpdateExposurePolicyUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_SavesValidatedPolicyAndWritesAudit()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var useCase = new UpdateExposurePolicyUseCase(
            settings,
            audit,
            new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault()));
        var policy = new ExposurePolicy(
            enabledTools: ["get_player_context"],
            enabledResources: ["ffxiv://player/context"],
            enabledAddons: ["Inventory"]);

        await useCase.ExecuteAsync(policy, CancellationToken.None);

        Assert.Equal(policy, settings.Policy);
        Assert.Single(audit.Events);
        Assert.Equal("settings.updated", audit.Events[0].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForUnknownTool_AndDoesNotSave()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var useCase = new UpdateExposurePolicyUseCase(
            settings,
            audit,
            new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault()));
        var policy = new ExposurePolicy(enabledTools: ["not_known"]);

        var action = () => useCase.ExecuteAsync(policy, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(ExposurePolicy.Default, settings.Policy);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForDeniedTool_AndDoesNotSave()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var registry = new CapabilityRegistry(
            [
                new CapabilityDefinition(
                    new CapabilityId("system.denied"),
                    "Denied Tool",
                    "Denied for testing.",
                    CapabilityCategory.System,
                    SensitivityLevel.Blocked,
                    ProfileType.Observation,
                    defaultEnabled: false,
                    requiresConsent: false,
                    denied: true,
                    supportsTool: true,
                    supportsResource: false,
                    version: "1.0.0")
            ],
            [
                new ToolBinding(new CapabilityId("system.denied"), "blocked_tool", "blocked.input", "blocked.output", "BlockedToolHandler", true)
            ],
            [],
            []);
        var useCase = new UpdateExposurePolicyUseCase(
            settings,
            audit,
            new SettingsMutationGuard(registry));
        var policy = new ExposurePolicy(enabledTools: ["blocked_tool"]);

        var action = () => useCase.ExecuteAsync(policy, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(ExposurePolicy.Default, settings.Policy);
        Assert.Empty(audit.Events);
    }
}
