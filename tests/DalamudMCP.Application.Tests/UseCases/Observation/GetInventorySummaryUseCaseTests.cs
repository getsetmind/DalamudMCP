using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetInventorySummaryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenToolEnabledAndSnapshotExists()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 19, 12, 0, 10, TimeSpan.Zero) };
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default.EnableTool("get_inventory_summary"),
        };
        var reader = new FakeInventorySummaryReader
        {
            Snapshot = new InventorySummarySnapshot(
                clock.UtcNow.AddSeconds(-1),
                123456,
                40,
                140,
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["crystals"] = 10 },
                "Inventory summary"),
        };
        var useCase = new GetInventorySummaryUseCase(reader, settings, KnownCapabilityRegistry.CreateDefault(), new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(QueryStatus.Success, result.Status);
        Assert.Equal(123456, result.Value!.CurrencyGil);
        Assert.Equal(1000, result.SnapshotAgeMs);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotReady_WhenSnapshotMissing()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default.EnableTool("get_inventory_summary"),
        };
        var useCase = new GetInventorySummaryUseCase(
            new FakeInventorySummaryReader(),
            settings,
            KnownCapabilityRegistry.CreateDefault(),
            new SnapshotFreshnessPolicy(new FakeClock()));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(QueryStatus.NotReady, result.Status);
        Assert.Equal("inventory_not_ready", result.Reason);
    }
}
