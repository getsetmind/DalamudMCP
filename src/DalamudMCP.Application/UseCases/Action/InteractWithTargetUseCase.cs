using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Action;

public sealed class InteractWithTargetUseCase
{
    private const string ToolName = "interact_with_target";

    private readonly IInteractWithTargetController controller;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public InteractWithTargetUseCase(
        IInteractWithTargetController controller,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.controller = controller;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<QueryResult<InteractWithTargetResult>> ExecuteAsync(
        string? expectedGameObjectId,
        bool checkLineOfSight,
        CancellationToken cancellationToken)
    {
        if (!capabilityRegistry.TryGetToolBinding(ToolName, out var binding) || binding is null)
        {
            return QueryResults.Denied<InteractWithTargetResult>("capability_missing");
        }

        if (!capabilityRegistry.TryGetCapability(binding.CapabilityId.Value, out var capability) || capability is null || capability.Denied)
        {
            return QueryResults.Denied<InteractWithTargetResult>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.CanExposeTool(capability, ToolName))
        {
            return QueryResults.Disabled<InteractWithTargetResult>("tool_disabled");
        }

        var result = await controller.InteractAsync(expectedGameObjectId, checkLineOfSight, cancellationToken).ConfigureAwait(false);
        return QueryResults.Success(result, DateTimeOffset.UtcNow, 0);
    }
}
