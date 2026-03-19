using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Action;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Action;

public sealed class SendAddonCallbackValuesUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsControllerResult_WhenActionProfileAndAddonAreEnabled()
    {
        var controller = new FakeAddonCallbackController
        {
            ValuesResult = new AddonCallbackValuesResult(
                "TelepotTown",
                [3, 0],
                Succeeded: true,
                Reason: null,
                SummaryText: "Sent callback values."),
        };
        var useCase = new SendAddonCallbackValuesUseCase(
            controller,
            new InMemorySettingsRepository
            {
                Policy = new Domain.Policy.ExposurePolicy(
                    enabledTools: ["send_addon_callback_values"],
                    enabledAddons: ["TelepotTown"],
                    actionProfileEnabled: true),
            },
            KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync("TelepotTown", [3, 0], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("TelepotTown", controller.LastAddonName);
        Assert.Equal([3, 0], controller.LastValues);
        Assert.True(result.Value!.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDisabled_WhenAddonIsNotEnabled()
    {
        var useCase = new SendAddonCallbackValuesUseCase(
            new FakeAddonCallbackController(),
            new InMemorySettingsRepository
            {
                Policy = new Domain.Policy.ExposurePolicy(
                    enabledTools: ["send_addon_callback_values"],
                    actionProfileEnabled: true),
            },
            KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync("TelepotTown", [3, 0], TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("addon_disabled", result.Reason);
    }
}
