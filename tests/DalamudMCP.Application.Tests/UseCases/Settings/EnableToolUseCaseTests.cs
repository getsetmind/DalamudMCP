using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Settings;

public sealed class EnableToolUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_SavesPolicyAndWritesAudit()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var useCase = new EnableToolUseCase(settings, audit, new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault()));

        await useCase.ExecuteAsync("get_player_context", CancellationToken.None);

        Assert.Contains("get_player_context", settings.Policy.EnabledTools);
        Assert.Single(audit.Events);
        Assert.Equal("tool.enabled", audit.Events[0].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForUnknownTool_AndDoesNotPersist()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var useCase = new EnableToolUseCase(settings, audit, new SettingsMutationGuard(KnownCapabilityRegistry.CreateDefault()));

        var action = () => useCase.ExecuteAsync("unknown_tool", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Empty(settings.Policy.EnabledTools);
        Assert.Empty(audit.Events);
    }
}
