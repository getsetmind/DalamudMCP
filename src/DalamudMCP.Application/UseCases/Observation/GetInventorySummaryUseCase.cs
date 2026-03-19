using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetInventorySummaryUseCase
{
    private const string ToolName = "get_inventory_summary";
    private const string ResourceUri = "ffxiv://inventory/summary";
    private static readonly CapabilityId CapabilityId = new("inventory.summary");

    private readonly IInventorySummaryReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetInventorySummaryUseCase(
        IInventorySummaryReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public Task<QueryResult<InventorySummarySnapshot>> ExecuteAsync(CancellationToken cancellationToken) =>
        ExecuteForToolAsync(cancellationToken);

    public async Task<QueryResult<InventorySummarySnapshot>> ExecuteForToolAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<InventorySummarySnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<InventorySummarySnapshot>("tool_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryResult<InventorySummarySnapshot>> ExecuteForResourceAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<InventorySummarySnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeResource(policy, capability, ResourceUri))
        {
            return QueryResults.Disabled<InventorySummarySnapshot>("resource_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult<InventorySummarySnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await reader.ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return QueryResults.NotReady<InventorySummarySnapshot>("inventory_not_ready");
        }

        var freshness = snapshotFreshnessPolicy.Evaluate(snapshot.CapturedAt);
        return QueryResults.Success(snapshot, snapshot.CapturedAt, freshness.AgeMs);
    }
}
