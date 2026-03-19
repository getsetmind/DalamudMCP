using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetAddonTreeUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenToolAndAddonAreEnabled()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 19, 12, 5, 0, TimeSpan.Zero) };
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default
                .EnableTool("get_addon_tree")
                .EnableAddon("Inventory"),
        };
        var reader = new FakeAddonTreeReader();
        reader.Set(
            "Inventory",
            new AddonTreeSnapshot(
                "Inventory",
                clock.UtcNow.AddSeconds(-3),
                [
                    new NodeSnapshot(1, "Component", true, 10, 20, 30, 40, "Root", []),
                ]));
        var useCase = new GetAddonTreeUseCase(reader, settings, KnownCapabilityRegistry.CreateDefault(), new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync("Inventory", CancellationToken.None);

        Assert.Equal(QueryStatus.Success, result.Status);
        Assert.Single(result.Value!.Roots);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenAddonIsNotEnabled()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default.EnableTool("get_addon_tree"),
        };
        var useCase = new GetAddonTreeUseCase(
            new FakeAddonTreeReader(),
            settings,
            KnownCapabilityRegistry.CreateDefault(),
            new SnapshotFreshnessPolicy(new FakeClock()));

        var result = await useCase.ExecuteAsync("Inventory", CancellationToken.None);

        Assert.Equal(QueryStatus.Disabled, result.Status);
        Assert.Equal("addon_disabled", result.Reason);
    }
}
