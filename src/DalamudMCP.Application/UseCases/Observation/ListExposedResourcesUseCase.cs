using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class ListExposedResourcesUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public ListExposedResourcesUseCase(
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<IReadOnlyList<ResourceBinding>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);

        return capabilityRegistry.ResourceBindings
            .Where(binding =>
            {
                var capability = capabilityRegistry.Capabilities.Single(cap => cap.Id == binding.CapabilityId);
                return ExposurePolicyEvaluator.CanExposeResource(policy, capability, binding.UriTemplate);
            })
            .ToArray();
    }
}
