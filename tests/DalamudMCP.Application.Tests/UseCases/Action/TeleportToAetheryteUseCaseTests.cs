using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Action;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Action;

public sealed class TeleportToAetheryteUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsControllerResult_WhenActionProfileIsEnabled()
    {
        var controller = new FakeAetheryteTeleportController
        {
            Result = new TeleportToAetheryteResult(
                "gold saucer",
                Succeeded: true,
                Reason: null,
                AetheryteId: 144U,
                AetheryteName: "The Gold Saucer",
                TerritoryName: "The Gold Saucer",
                SummaryText: "Teleport started to The Gold Saucer."),
        };
        var useCase = new TeleportToAetheryteUseCase(
            controller,
            new InMemorySettingsRepository
            {
                Policy = new Domain.Policy.ExposurePolicy(
                    enabledTools: ["teleport_to_aetheryte"],
                    actionProfileEnabled: true),
            },
            KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync("gold saucer", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("gold saucer", controller.LastQuery);
        Assert.True(result.Value!.Succeeded);
        Assert.Equal((uint)144, result.Value.AetheryteId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenActionProfileIsDisabled()
    {
        var useCase = new TeleportToAetheryteUseCase(
            new FakeAetheryteTeleportController(),
            new InMemorySettingsRepository
            {
                Policy = new Domain.Policy.ExposurePolicy(enabledTools: ["teleport_to_aetheryte"], actionProfileEnabled: false),
            },
            KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync("gold saucer", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_disabled", result.Reason);
    }
}
