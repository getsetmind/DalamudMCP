using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Settings;

public sealed class EnableResourceUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EnablesKnownResource_AndWritesAudit()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var useCase = new EnableResourceUseCase(
            settings,
            audit,
            new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault()));

        await useCase.ExecuteAsync("ffxiv://player/context", CancellationToken.None);

        Assert.Contains("ffxiv://player/context", settings.Policy.EnabledResources);
        Assert.Equal("resource.enabled", Assert.Single(audit.Events).EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForDeniedResource_AndDoesNotWrite()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var registry = new CapabilityRegistry(
            [
                new CapabilityDefinition(
                    new CapabilityId("ui.secret"),
                    "Secret UI",
                    "Denied for testing.",
                    CapabilityCategory.Ui,
                    SensitivityLevel.Blocked,
                    ProfileType.Observation,
                    defaultEnabled: false,
                    requiresConsent: false,
                    denied: true,
                    supportsTool: false,
                    supportsResource: true,
                    version: "1.0.0")
            ],
            [],
            [
                new ResourceBinding(new CapabilityId("ui.secret"), "ffxiv://ui/secret", "application/json", "SecretResourceProvider", true)
            ],
            []);
        var useCase = new EnableResourceUseCase(settings, audit, new SettingsMutationGuard(registry));

        var action = () => useCase.ExecuteAsync("ffxiv://ui/secret", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Empty(settings.Policy.EnabledResources);
        Assert.Empty(audit.Events);
    }
}
