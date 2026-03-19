using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Observation;

public sealed class GetNearbyInteractablesUseCase
{
    private const string ToolName = "get_nearby_interactables";
    private static readonly CapabilityId CapabilityId = new("world.nearbyInteractables");

    private readonly INearbyInteractablesReader reader;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;
    private readonly SnapshotFreshnessPolicy snapshotFreshnessPolicy;

    public GetNearbyInteractablesUseCase(
        INearbyInteractablesReader reader,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy)
    {
        this.reader = reader;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
        this.snapshotFreshnessPolicy = snapshotFreshnessPolicy;
    }

    public async Task<QueryResult<NearbyInteractablesSnapshot>> ExecuteForToolAsync(
        double? maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken)
    {
        var capability = capabilityRegistry.Capabilities.Single(static capability => capability.Id == CapabilityId);
        if (capability.Denied)
        {
            return QueryResults.Denied<NearbyInteractablesSnapshot>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!ExposurePolicyEvaluator.CanExposeTool(policy, capability, ToolName))
        {
            return QueryResults.Disabled<NearbyInteractablesSnapshot>("tool_disabled");
        }

        var effectiveMaxDistance = NormalizeMaxDistance(maxDistance);
        var snapshot = await reader.ReadCurrentAsync(
            effectiveMaxDistance,
            NormalizeNameFilter(nameContains),
            includePlayers,
            cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return QueryResults.NotReady<NearbyInteractablesSnapshot>("interactables_not_ready");
        }

        var freshness = snapshotFreshnessPolicy.Evaluate(snapshot.CapturedAt);
        return QueryResults.Success(snapshot, snapshot.CapturedAt, freshness.AgeMs);
    }

    private static double NormalizeMaxDistance(double? maxDistance)
    {
        if (maxDistance is null)
        {
            return 8d;
        }

        if (double.IsNaN(maxDistance.Value) || double.IsInfinity(maxDistance.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistance), "Max distance must be finite.");
        }

        return Math.Clamp(maxDistance.Value, 1d, 40d);
    }

    private static string? NormalizeNameFilter(string? nameContains) =>
        string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
}
