using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.Tests.UseCases.Settings;

public sealed class ApplyPresetUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_Recommended_EnablesBaselineCapabilitiesAndRecommendedAddons()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var registry = KnownCapabilityRegistry.CreateDefault();
        var useCase = new ApplyPresetUseCase(settings, audit, registry, new SettingsMutationGuard(registry));

        var policy = await useCase.ExecuteAsync(ExposurePreset.Recommended, CancellationToken.None);

        Assert.Equal(["get_duty_context", "get_inventory_summary", "get_player_context"], policy.EnabledTools.OrderBy(static value => value).ToArray());
        Assert.Equal(["ffxiv://duty/context", "ffxiv://inventory/summary", "ffxiv://player/context"], policy.EnabledResources.OrderBy(static value => value).ToArray());
        Assert.Equal(["Character", "Inventory"], policy.EnabledAddons.OrderBy(static value => value).ToArray());
        Assert.False(policy.ActionProfileEnabled);
        Assert.Equal("settings.preset_applied", Assert.Single(audit.Events).EventType);
    }

    [Fact]
    public async Task ExecuteAsync_UiExplorer_EnablesUiObservationSurface()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var registry = KnownCapabilityRegistry.CreateDefault();
        var useCase = new ApplyPresetUseCase(settings, audit, registry, new SettingsMutationGuard(registry));

        var policy = await useCase.ExecuteAsync(ExposurePreset.UiExplorer, CancellationToken.None);

        Assert.Contains("get_addon_list", policy.EnabledTools);
        Assert.Contains("ffxiv://ui/addons", policy.EnabledResources);
        Assert.Contains("get_addon_tree", policy.EnabledTools);
        Assert.Contains("get_addon_strings", policy.EnabledTools);
        Assert.Contains("ffxiv://ui/addon/{addonName}/tree", policy.EnabledResources);
        Assert.Contains("ffxiv://ui/addon/{addonName}/strings", policy.EnabledResources);
        Assert.Contains("ContentsInfoDetail", policy.EnabledAddons);
    }

    [Fact]
    public async Task ExecuteAsync_LockedDown_OnlyEnablesPlayerContext()
    {
        var settings = new InMemorySettingsRepository();
        var audit = new InMemoryAuditLogWriter();
        var registry = KnownCapabilityRegistry.CreateDefault();
        var useCase = new ApplyPresetUseCase(settings, audit, registry, new SettingsMutationGuard(registry));

        var policy = await useCase.ExecuteAsync(ExposurePreset.LockedDown, CancellationToken.None);

        Assert.Equal(["get_player_context"], policy.EnabledTools.OrderBy(static value => value).ToArray());
        Assert.Equal(["ffxiv://player/context"], policy.EnabledResources.OrderBy(static value => value).ToArray());
        Assert.Empty(policy.EnabledAddons);
    }
}
