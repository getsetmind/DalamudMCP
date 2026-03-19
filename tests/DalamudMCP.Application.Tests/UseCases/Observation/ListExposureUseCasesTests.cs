using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Observation;

public sealed class ListExposureUseCasesTests
{
    [Fact]
    public async Task ListExposedTools_ReturnsOnlyEnabledTools()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default
                .EnableTool("get_player_context")
                .EnableTool("get_addon_tree"),
        };
        var useCase = new ListExposedToolsUseCase(settings, KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["get_addon_tree", "get_player_context"], result.Select(static binding => binding.ToolName).OrderBy(static value => value).ToArray());
    }

    [Fact]
    public async Task ListExposedResources_ReturnsOnlyEnabledResources()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default
                .EnableResource("ffxiv://player/context")
                .EnableResource("ffxiv://inventory/summary"),
        };
        var useCase = new ListExposedResourcesUseCase(settings, KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["ffxiv://inventory/summary", "ffxiv://player/context"], result.Select(static binding => binding.UriTemplate).OrderBy(static value => value).ToArray());
    }

    [Fact]
    public async Task ListInspectableAddons_ExcludesDisabledAddons()
    {
        var settings = new InMemorySettingsRepository
        {
            Policy = ExposurePolicy.Default.EnableAddon("Inventory"),
        };
        var useCase = new ListInspectableAddonsUseCase(settings, KnownCapabilityRegistry.CreateDefault());

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        var addon = Assert.Single(result);
        Assert.Equal("Inventory", addon.AddonName);
    }
}
