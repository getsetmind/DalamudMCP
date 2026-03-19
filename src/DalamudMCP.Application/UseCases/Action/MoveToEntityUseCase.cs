using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Action;

public sealed class MoveToEntityUseCase
{
    private const string ToolName = "move_to_entity";

    private readonly IEntityMovementController controller;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public MoveToEntityUseCase(
        IEntityMovementController controller,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.controller = controller;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<QueryResult<MoveToEntityResult>> ExecuteAsync(
        string gameObjectId,
        bool allowFlight,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameObjectId);

        if (!capabilityRegistry.TryGetToolBinding(ToolName, out var binding) || binding is null)
        {
            return QueryResults.Denied<MoveToEntityResult>("capability_missing");
        }

        if (!capabilityRegistry.TryGetCapability(binding.CapabilityId.Value, out var capability) || capability is null || capability.Denied)
        {
            return QueryResults.Denied<MoveToEntityResult>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.CanExposeTool(capability, ToolName))
        {
            return QueryResults.Disabled<MoveToEntityResult>("tool_disabled");
        }

        var result = await controller.MoveToEntityAsync(gameObjectId, allowFlight, cancellationToken).ConfigureAwait(false);
        return QueryResults.Success(result, DateTimeOffset.UtcNow, 0);
    }
}
