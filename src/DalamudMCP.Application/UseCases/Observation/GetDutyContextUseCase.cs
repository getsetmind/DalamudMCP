using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetDutyContextUseCase
{
    private const string ToolName = "get_duty_context";
    private const string ResourceUri = "ffxiv://duty/context";
    private static readonly CapabilityId CapabilityId = new("duty.context");

    private readonly IDutyContextReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetDutyContextUseCase(
        IDutyContextReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public Task<QueryResult<DutyContextSnapshot>> ExecuteAsync(CancellationToken cancellationToken) =>
        ExecuteForToolAsync(cancellationToken);

    public async Task<QueryResult<DutyContextSnapshot>> ExecuteForToolAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<DutyContextSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<DutyContextSnapshot>("tool_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryResult<DutyContextSnapshot>> ExecuteForResourceAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<DutyContextSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeResource(policy, capability, ResourceUri))
        {
            return QueryResults.Disabled<DutyContextSnapshot>("resource_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult<DutyContextSnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await reader.ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return QueryResults.NotReady<DutyContextSnapshot>("duty_not_ready");
        }

        var freshness = snapshotFreshnessPolicy.Evaluate(snapshot.CapturedAt);
        return QueryResults.Success(snapshot, snapshot.CapturedAt, freshness.AgeMs);
    }
}
