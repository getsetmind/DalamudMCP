using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetAddonStringsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenMatchingCapabilityExists()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 19, 12, 6, 0, TimeSpan.Zero) };
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default
                .EnableTool("get_addon_strings")
                .EnableAddon("Inventory"),
        };
        var reader = new FakeStringTableReader();
        reader.Set(
            "Inventory",
            new StringTableSnapshot(
                "Inventory",
                clock.UtcNow.AddSeconds(-2),
                [
                    new StringTableEntry(0, "raw", "decoded"),
                ]));
        var useCase = new GetAddonStringsUseCase(reader, settings, KnownCapabilityRegistry.CreateDefault(), new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync("Inventory", CancellationToken.None);

        Assert.Equal(QueryStatus.Success, result.Status);
        Assert.Equal("decoded", Assert.Single(result.Value!.Entries).DecodedValue);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotReady_WhenSnapshotMissing()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default
                .EnableTool("get_addon_strings")
                .EnableAddon("Inventory"),
        };
        var useCase = new GetAddonStringsUseCase(
            new FakeStringTableReader(),
            settings,
            KnownCapabilityRegistry.CreateDefault(),
            new SnapshotFreshnessPolicy(new FakeClock()));

        var result = await useCase.ExecuteAsync("Inventory", CancellationToken.None);

        Assert.Equal(QueryStatus.NotReady, result.Status);
        Assert.Equal("addon_not_ready", result.Reason);
    }
}
