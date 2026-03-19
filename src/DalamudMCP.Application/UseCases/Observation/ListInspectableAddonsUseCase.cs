using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class ListInspectableAddonsUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public ListInspectableAddonsUseCase(
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<IReadOnlyList<AddonMetadata>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        return capabilityRegistry.Addons
            .Where(addon => !addon.Denied && ExposurePolicyEvaluator.CanInspectAddon(policy, addon.AddonName))
            .ToArray();
    }
}
