using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class GetAddonListUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsNotReady_WhenNoAddonsExist()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = new Domain.Policy.ExposurePolicy(enabledTools: ["get_addon_list"], enabledResources: [], enabledAddons: []),
        };
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var useCase = new GetAddonListUseCase(
            new FakeAddonCatalogReader(),
            settings,
            CreateRegistry(),
            new SnapshotFreshnessPolicy(clock));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(DalamudMCP.Application.Common.QueryStatus.NotReady, result.Status);
    }

    private static CapabilityRegistry CreateRegistry()
    {
        return new CapabilityRegistry(
            [
                new CapabilityDefinition(
                    new CapabilityId("ui.addonCatalog"),
                    "Addon List",
                    "Read addon list.",
                    CapabilityCategory.Ui,
                    SensitivityLevel.High,
                    ProfileType.Observation,
                    defaultEnabled: false,
                    requiresConsent: true,
                    denied: false,
                    supportsTool: true,
                    supportsResource: true,
                    version: "1.0.0"),
            ],
            [
                new ToolBinding(new CapabilityId("ui.addonCatalog"), "get_addon_list", "schemas.in", "schemas.out", "handler", false),
            ],
            [],
            []);
    }
}
