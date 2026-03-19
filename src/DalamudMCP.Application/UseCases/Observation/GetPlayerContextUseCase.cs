using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetPlayerContextUseCase
{
    private const string ToolName = "get_player_context";
    private const string ResourceUri = "ffxiv://player/context";
    private static readonly CapabilityId CapabilityId = new("player.context");

    private readonly IPlayerContextReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetPlayerContextUseCase(
        IPlayerContextReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public Task<QueryResult<PlayerContextSnapshot>> ExecuteAsync(CancellationToken cancellationToken) =>
        ExecuteForToolAsync(cancellationToken);

    public async Task<QueryResult<PlayerContextSnapshot>> ExecuteForToolAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<PlayerContextSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<PlayerContextSnapshot>("tool_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryResult<PlayerContextSnapshot>> ExecuteForResourceAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<PlayerContextSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeResource(policy, capability, ResourceUri))
        {
            return QueryResults.Disabled<PlayerContextSnapshot>("resource_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult<PlayerContextSnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await reader.ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return QueryResults.NotReady<PlayerContextSnapshot>("player_not_ready");
        }

        var freshness = snapshotFreshnessPolicy.Evaluate(snapshot.CapturedAt);
        return QueryResults.Success(snapshot, snapshot.CapturedAt, freshness.AgeMs);
    }
}
