using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Audit;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class ApplyPresetUseCase
{
    private static readonly HashSet<string> RecommendedCapabilityIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "player.context",
        "duty.context",
        "inventory.summary",
    };

    private static readonly HashSet<string> UiExplorerCapabilityIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "player.context",
        "duty.context",
        "inventory.summary",
        "ui.addonCatalog",
        "ui.addonTree",
        "ui.stringTable",
    };

    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SettingsMutationGuard mutationGuard;

    public ApplyPresetUseCase(
        ISettingsRepository settingsRepository,
        IAuditLogWriter auditLogWriter,
        CapabilityRegistry capabilityRegistry,
        SettingsMutationGuard mutationGuard)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
        this.capabilityRegistry = capabilityRegistry;
        this.mutationGuard = mutationGuard;
    }

    public async Task<ExposurePolicy> ExecuteAsync(ExposurePreset preset, CancellationToken cancellationToken)
    {
        _ = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);

        var policy = preset switch
        {
            ExposurePreset.Recommended => CreateRecommendedPolicy(),
            ExposurePreset.UiExplorer => CreateUiExplorerPolicy(),
            ExposurePreset.LockedDown => CreateLockedDownPolicy(),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported preset."),
        };

        mutationGuard.EnsurePolicyAllowed(policy);
        await settingsRepository.SaveAsync(policy, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(
            new AuditEvent(DateTimeOffset.UtcNow, "settings.preset_applied", preset.ToString()),
            cancellationToken).ConfigureAwait(false);
        return policy;
    }

    private ExposurePolicy CreateRecommendedPolicy() =>
        ExposurePolicy.Default
            .ReplaceSelections(
                capabilityRegistry.ToolBindings
                    .Where(binding => RecommendedCapabilityIds.Contains(binding.CapabilityId.Value))
                    .Select(static binding => binding.ToolName),
                capabilityRegistry.ResourceBindings
                    .Where(binding => RecommendedCapabilityIds.Contains(binding.CapabilityId.Value))
                    .Select(static binding => binding.UriTemplate),
                capabilityRegistry.Addons
                    .Where(static addon => addon.Recommended && !addon.Denied)
                    .Select(static addon => addon.AddonName))
            .WithProfiles(observationProfileEnabled: true, actionProfileEnabled: false);

    private ExposurePolicy CreateUiExplorerPolicy() =>
        ExposurePolicy.Default
            .ReplaceSelections(
                capabilityRegistry.ToolBindings
                    .Where(binding => UiExplorerCapabilityIds.Contains(binding.CapabilityId.Value))
                    .Select(static binding => binding.ToolName),
                capabilityRegistry.ResourceBindings
                    .Where(binding => UiExplorerCapabilityIds.Contains(binding.CapabilityId.Value))
                    .Select(static binding => binding.UriTemplate),
                capabilityRegistry.Addons
                    .Where(addon => !addon.Denied && addon.Sensitivity is SensitivityLevel.Low or SensitivityLevel.Medium)
                    .Select(static addon => addon.AddonName))
            .WithProfiles(observationProfileEnabled: true, actionProfileEnabled: false);

    private ExposurePolicy CreateLockedDownPolicy() =>
        ExposurePolicy.Default
            .ReplaceSelections(
                capabilityRegistry.ToolBindings
                    .Where(static binding => binding.CapabilityId.Value == "player.context")
                    .Select(static binding => binding.ToolName),
                capabilityRegistry.ResourceBindings
                    .Where(static binding => binding.CapabilityId.Value == "player.context")
                    .Select(static binding => binding.UriTemplate),
                [])
            .WithProfiles(observationProfileEnabled: true, actionProfileEnabled: false);
}
