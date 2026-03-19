using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetDutyContextUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenToolEnabledAndSnapshotExists()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 19, 12, 0, 3, TimeSpan.Zero) };
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default.EnableTool("get_duty_context"),
        };
        var reader = new FakeDutyContextReader
        {
            Snapshot = new DutyContextSnapshot(clock.UtcNow.AddSeconds(-2), 777, "The Praetorium", "Dungeon", true, false, "In duty"),
        };
        var useCase = new GetDutyContextUseCase(reader, settings, KnownCapabilityRegistry.CreateDefault(), new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(QueryStatus.Success, result.Status);
        Assert.Equal("The Praetorium", result.Value!.DutyName);
        Assert.Equal(2000, result.SnapshotAgeMs);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenToolNotEnabled()
    {
        var useCase = new GetDutyContextUseCase(
            new FakeDutyContextReader(),
            new InMemorySettingsRepository(),
            KnownCapabilityRegistry.CreateDefault(),
            new SnapshotFreshnessPolicy(new FakeClock()));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(QueryStatus.Disabled, result.Status);
        Assert.Equal("tool_disabled", result.Reason);
    }
}
