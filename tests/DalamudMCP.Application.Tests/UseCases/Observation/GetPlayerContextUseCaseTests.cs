using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetPlayerContextUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenToolNotEnabled()
    {
        var settings = new InMemorySettingsRepository();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var useCase = new GetPlayerContextUseCase(
            new FakePlayerContextReader(),
            settings,
            CreateRegistry(),
            new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(DalamudMCP.Application.Common.QueryStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenToolEnabledAndSnapshotAvailable()
    {
        var capturedAt = new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero);
        var settings = new InMemorySettingsRepository
        {
            Policy = settingsFromTools("get_player_context"),
        };
        var reader = new FakePlayerContextReader
        {
            Snapshot = new PlayerContextSnapshot(
                capturedAt,
                "Alice",
                "Tonberry",
                "Tonberry",
                24,
                "White Mage",
                100,
                1,
                "Limsa",
                2,
                "Lower Decks",
                new PositionSnapshot(1, 2, 3, "coarse"),
                false,
                false,
                false,
                false,
                false,
                false,
                "city",
                "idle",
                "Lv100 White Mage"),
        };
        var clock = new FakeClock { UtcNow = capturedAt.AddMilliseconds(500) };
        var useCase = new GetPlayerContextUseCase(
            reader,
            settings,
            CreateRegistry(),
            new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(500, result.SnapshotAgeMs);
        Assert.Equal("Alice", result.Value?.CharacterName);
    }

    [Fact]
    public async Task ExecuteForResourceAsync_ReturnsSuccess_WhenResourceEnabledAndSnapshotAvailable()
    {
        var capturedAt = new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero);
        var settings = new InMemorySettingsRepository
        {
            Policy = new Domain.Policy.ExposurePolicy(
                enabledTools: [],
                enabledResources: ["ffxiv://player/context"],
                enabledAddons: []),
        };
        var reader = new FakePlayerContextReader
        {
            Snapshot = new PlayerContextSnapshot(
                capturedAt,
                "Alice",
                "Tonberry",
                "Tonberry",
                24,
                "White Mage",
                100,
                1,
                "Limsa",
                2,
                "Lower Decks",
                new PositionSnapshot(1, 2, 3, "coarse"),
                false,
                false,
                false,
                false,
                false,
                false,
                "city",
                "idle",
                "Lv100 White Mage"),
        };
        var clock = new FakeClock { UtcNow = capturedAt.AddMilliseconds(250) };
        var useCase = new GetPlayerContextUseCase(
            reader,
            settings,
            CreateRegistry(),
            new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteForResourceAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(250, result.SnapshotAgeMs);
        Assert.Equal("Alice", result.Value?.CharacterName);
    }

    private static CapabilityRegistry CreateRegistry()
    {
        return new CapabilityRegistry(
            [
                new CapabilityDefinition(
                    new CapabilityId("player.context"),
                    "Player Context",
                    "Read player context.",
                    CapabilityCategory.Player,
                    SensitivityLevel.Low,
                    ProfileType.Observation,
                    defaultEnabled: true,
                    requiresConsent: false,
                    denied: false,
                    supportsTool: true,
                    supportsResource: true,
                    version: "1.0.0"),
            ],
            [
                new ToolBinding(new CapabilityId("player.context"), "get_player_context", "schemas.in", "schemas.out", "handler", false),
            ],
            [
                new ResourceBinding(new CapabilityId("player.context"), "ffxiv://player/context", "application/json", "handler", false),
            ],
            []);
    }

    private static Domain.Policy.ExposurePolicy settingsFromTools(params string[] tools) =>
        new(enabledTools: tools, enabledResources: [], enabledAddons: []);
}
