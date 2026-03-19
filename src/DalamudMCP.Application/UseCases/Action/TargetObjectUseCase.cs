using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Action;

public sealed class TargetObjectUseCase
{
    private const string ToolName = "target_object";
    private static readonly CapabilityId CapabilityId = new("world.targetObject");

    private readonly ITargetObjectController controller;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public TargetObjectUseCase(
        ITargetObjectController controller,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.controller = controller;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<QueryResult<TargetObjectResult>> ExecuteAsync(string gameObjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameObjectId);

        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<TargetObjectResult>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<TargetObjectResult>("tool_disabled");
        }

        var result = await controller.TargetAsync(gameObjectId.Trim(), cancellationToken).ConfigureAwait(false);
        return QueryResults.Success(result, DateTimeOffset.UtcNow, 0);
    }
}
