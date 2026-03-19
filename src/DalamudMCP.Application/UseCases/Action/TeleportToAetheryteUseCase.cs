using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Action;

public sealed class TeleportToAetheryteUseCase
{
    private const string ToolName = "teleport_to_aetheryte";
    private static readonly CapabilityId CapabilityId = new("world.teleportToAetheryte");

    private readonly IAetheryteTeleportController controller;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public TeleportToAetheryteUseCase(
        IAetheryteTeleportController controller,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.controller = controller;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<QueryResult<TeleportToAetheryteResult>> ExecuteAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<TeleportToAetheryteResult>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<TeleportToAetheryteResult>("tool_disabled");
        }

        var result = await controller.TeleportAsync(query.Trim(), cancellationToken).ConfigureAwait(false);
        return QueryResults.Success(result, DateTimeOffset.UtcNow, 0);
    }
}
