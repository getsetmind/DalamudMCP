using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class ListExposedToolsUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public ListExposedToolsUseCase(
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<IReadOnlyList<ToolBinding>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);

        return capabilityRegistry.ToolBindings
            .Where(binding =>
            {
                var capability = capabilityRegistry.Capabilities.Single(cap => cap.Id == binding.CapabilityId);
                return ExposurePolicyEvaluator.CanExposeTool(policy, capability, binding.ToolName);
            })
            .ToArray();
    }
}
