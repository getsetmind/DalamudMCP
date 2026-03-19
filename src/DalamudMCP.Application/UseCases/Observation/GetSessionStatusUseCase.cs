using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Session;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetSessionStatusUseCase
{
    private const string ToolName = "get_session_status";
    private const string ResourceUri = "ffxiv://session/status";
    private static readonly CapabilityId CapabilityId = new("session.status");

    private readonly ISessionStateReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetSessionStatusUseCase(
        ISessionStateReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public Task<QueryResult<SessionState>> ExecuteAsync(CancellationToken cancellationToken) =>
        ExecuteForToolAsync(cancellationToken);

    public async Task<QueryResult<SessionState>> ExecuteForToolAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<SessionState>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<SessionState>("tool_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryResult<SessionState>> ExecuteForResourceAsync(CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<SessionState>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeResource(policy, capability, ResourceUri))
        {
            return QueryResults.Disabled<SessionState>("resource_disabled");
        }

        return await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult<SessionState>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await reader.ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
        var freshness = snapshotFreshnessPolicy.Evaluate(snapshot.CapturedAt);
        return QueryResults.Success(snapshot, snapshot.CapturedAt, freshness.AgeMs);
    }
}
