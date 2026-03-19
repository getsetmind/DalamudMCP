using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetAddonTreeUseCase
{
    private const string ToolName = "get_addon_tree";
    private const string ResourceUri = "ffxiv://ui/addon/{addonName}/tree";
    private static readonly CapabilityId CapabilityId = new("ui.addonTree");

    private readonly IAddonTreeReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetAddonTreeUseCase(
        IAddonTreeReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public Task<QueryResult<AddonTreeSnapshot>> ExecuteAsync(string addonName, CancellationToken cancellationToken) =>
        ExecuteForToolAsync(addonName, cancellationToken);

    public async Task<QueryResult<AddonTreeSnapshot>> ExecuteForToolAsync(string addonName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<AddonTreeSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<AddonTreeSnapshot>("tool_disabled");
        }

        return await ReadSnapshotAsync(policy, addonName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryResult<AddonTreeSnapshot>> ExecuteForResourceAsync(string addonName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<AddonTreeSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeResource(policy, capability, ResourceUri))
        {
            return QueryResults.Disabled<AddonTreeSnapshot>("resource_disabled");
        }

        return await ReadSnapshotAsync(policy, addonName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult<AddonTreeSnapshot>> ReadSnapshotAsync(
        Domain.Policy.ExposurePolicy policy,
        string addonName,
        CancellationToken cancellationToken)
    {
        if (!ExposurePolicyEvaluator.CanInspectAddon(policy, addonName))
        {
            return QueryResults.Disabled<AddonTreeSnapshot>("addon_disabled");
        }

        var snapshot = await reader.ReadCurrentAsync(addonName, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return QueryResults.NotReady<AddonTreeSnapshot>("addon_not_ready");
        }

        var freshness = snapshotFreshnessPolicy.Evaluate(snapshot.CapturedAt);
        return QueryResults.Success(snapshot, snapshot.CapturedAt, freshness.AgeMs);
    }
}
