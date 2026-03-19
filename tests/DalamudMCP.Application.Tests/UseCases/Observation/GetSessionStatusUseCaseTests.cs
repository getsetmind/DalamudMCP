using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Session;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetSessionStatusUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenToolNotEnabled()
    {
        var useCase = new GetSessionStatusUseCase(
            new FakeSessionStateReader(),
            new InMemorySettingsRepository(),
            CreateRegistry(),
            new SnapshotFreshnessPolicy(new FakeClock { UtcNow = DateTimeOffset.UtcNow }));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(DalamudMCP.Application.Common.QueryStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenToolEnabled()
    {
        var capturedAt = new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero);
        var reader = new FakeSessionStateReader
        {
            Snapshot = new SessionState(
                capturedAt,
                "DalamudMCP.TestPipe",
                true,
                [new SessionComponentState("player_context", false, "not_attached")],
                "0/1 readers ready; bridge server running."),
        };
        var settings = new InMemorySettingsRepository
        {
            Policy = new Domain.Policy.ExposurePolicy(enabledTools: ["get_session_status"], enabledResources: [], enabledAddons: []),
        };
        var useCase = new GetSessionStatusUseCase(
            reader,
            settings,
            CreateRegistry(),
            new SnapshotFreshnessPolicy(new FakeClock { UtcNow = capturedAt.AddMilliseconds(125) }));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(125, result.SnapshotAgeMs);
        Assert.Equal("DalamudMCP.TestPipe", result.Value?.PipeName);
        Assert.False(result.Value?.Components[0].IsReady);
    }

    [Fact]
    public async Task ExecuteForResourceAsync_ReturnsSuccess_WhenResourceEnabled()
    {
        var capturedAt = new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero);
        var reader = new FakeSessionStateReader
        {
            Snapshot = new SessionState(
                capturedAt,
                "DalamudMCP.TestPipe",
                false,
                [new SessionComponentState("player_context", false, "not_attached")],
                "0/1 readers ready; bridge server stopped."),
        };
        var settings = new InMemorySettingsRepository
        {
            Policy = new Domain.Policy.ExposurePolicy(enabledTools: [], enabledResources: ["ffxiv://session/status"], enabledAddons: []),
        };
        var useCase = new GetSessionStatusUseCase(
            reader,
            settings,
            CreateRegistry(),
            new SnapshotFreshnessPolicy(new FakeClock { UtcNow = capturedAt.AddMilliseconds(25) }));

        var result = await useCase.ExecuteForResourceAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.SnapshotAgeMs);
        Assert.False(result.Value?.IsBridgeServerRunning);
    }

    private static CapabilityRegistry CreateRegistry() =>
        new(
            [
                new CapabilityDefinition(
                    new CapabilityId("session.status"),
                    "Session Status",
                    "Read plugin status.",
                    CapabilityCategory.System,
                    SensitivityLevel.Low,
                    ProfileType.Observation,
                    defaultEnabled: false,
                    requiresConsent: false,
                    denied: false,
                    supportsTool: true,
                    supportsResource: true,
                    version: "1.0.0"),
            ],
            [
                new ToolBinding(new CapabilityId("session.status"), "get_session_status", "schemas.in", "schemas.out", "handler", false),
            ],
            [
                new ResourceBinding(new CapabilityId("session.status"), "ffxiv://session/status", "application/json", "handler", false),
            ],
            []);
}
