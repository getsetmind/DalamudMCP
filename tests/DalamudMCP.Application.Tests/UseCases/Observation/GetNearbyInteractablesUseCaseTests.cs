using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetNearbyInteractablesUseCaseTests
{
    [Fact]
    public async Task ExecuteForToolAsync_ReturnsSnapshot_WhenEnabled()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 20, 10, 0, 2, TimeSpan.Zero) };
        var reader = new FakeNearbyInteractablesReader
        {
            Snapshot = new NearbyInteractablesSnapshot(
                new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
                8d,
                [
                    new NearbyInteractable("0x123", "Triple Triad Table", "EventObj", true, 2.4d, 1d, new PositionSnapshot(1, 2, 3, "coarse")),
                ],
                "1 interactable objects within 8 yalms."),
        };
        var settings = new InMemorySettingsRepository
        {
            Policy = new Domain.Policy.ExposurePolicy(enabledTools: ["get_nearby_interactables"]),
        };
        var useCase = new GetNearbyInteractablesUseCase(
            reader,
            settings,
            KnownCapabilityRegistry.CreateDefault(),
            new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteForToolAsync(8d, "triad", includePlayers: false, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Interactables);
        Assert.Equal("Triple Triad Table", result.Value.Interactables[0].Name);
    }

    [Fact]
    public async Task ExecuteForToolAsync_ReturnsDisabled_WhenToolIsDisabled()
    {
        var useCase = new GetNearbyInteractablesUseCase(
            new FakeNearbyInteractablesReader(),
            new InMemorySettingsRepository
            {
                Policy = Domain.Policy.ExposurePolicy.Default,
            },
            KnownCapabilityRegistry.CreateDefault(),
            new SnapshotFreshnessPolicy(new FakeClock { UtcNow = DateTimeOffset.UtcNow }));

        var result = await useCase.ExecuteForToolAsync(null, null, includePlayers: false, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_disabled", result.Reason);
    }
}
