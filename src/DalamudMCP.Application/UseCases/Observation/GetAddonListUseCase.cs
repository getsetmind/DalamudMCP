using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetAddonListUseCase
{
    private const string ToolName = "get_addon_list";
    private const string ResourceUri = "ffxiv://ui/addons";
    private static readonly CapabilityId CapabilityId = new("ui.addonCatalog");

    private readonly IAddonCatalogReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetAddonListUseCase(
        IAddonCatalogReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public Task<QueryResult<IReadOnlyList<AddonSummary>>> ExecuteAsync(CancellationToken cancellationToken) =>
        ExecuteForToolAsync(cancellationToken);

    public async Task<QueryResult<IReadOnlyList<AddonSummary>>> ExecuteForToolAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<IReadOnlyList<AddonSummary>>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<IReadOnlyList<AddonSummary>>("tool_disabled");
        }

        return await ReadSnapshotsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryResult<IReadOnlyList<AddonSummary>>> ExecuteForResourceAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<IReadOnlyList<AddonSummary>>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeResource(policy, capability, ResourceUri))
        {
            return QueryResults.Disabled<IReadOnlyList<AddonSummary>>("resource_disabled");
        }

        return await ReadSnapshotsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult<IReadOnlyList<AddonSummary>>> ReadSnapshotsAsync(CancellationToken cancellationToken)
    {
        var snapshots = await reader.ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (snapshots.Count == 0)
        {
            return QueryResults.NotReady<IReadOnlyList<AddonSummary>>("addons_not_ready");
        }

        var capturedAt = snapshots.Max(static snapshot => snapshot.CapturedAt);
        var freshness = snapshotFreshnessPolicy.Evaluate(capturedAt);
        return QueryResults.Success<IReadOnlyList<AddonSummary>>(snapshots, capturedAt, freshness.AgeMs);
    }
}
