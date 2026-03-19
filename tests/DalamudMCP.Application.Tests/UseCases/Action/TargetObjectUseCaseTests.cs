using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Action;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Action;

public sealed class TargetObjectUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsControllerResult_WhenActionProfileIsEnabled()
    {
        var controller = new FakeTargetObjectController
        {
            Result = new TargetObjectResult(
                "0x123",
                Succeeded: true,
                Reason: null,
                TargetedGameObjectId: "0x123",
                TargetName: "Mahjong Table",
                ObjectKind: "EventObj",
                SummaryText: "Targeted Mahjong Table."),
        };
        var useCase = new TargetObjectUseCase(
            controller,
            new InMemorySettingsRepository
            {
                Policy = new Domain.Policy.ExposurePolicy(
                    enabledTools: ["target_object"],
                    actionProfileEnabled: true),
            },
            KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync("0x123", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("0x123", controller.LastGameObjectId);
        Assert.True(result.Value!.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenActionProfileIsDisabled()
    {
        var useCase = new TargetObjectUseCase(
            new FakeTargetObjectController(),
            new InMemorySettingsRepository
            {
                Policy = new Domain.Policy.ExposurePolicy(enabledTools: ["target_object"], actionProfileEnabled: false),
            },
            KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync("0x123", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_disabled", result.Reason);
    }
}
