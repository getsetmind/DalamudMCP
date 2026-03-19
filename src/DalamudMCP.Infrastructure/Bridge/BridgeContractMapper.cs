using DalamudMCP.Application.Common;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Responses;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Session;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Infrastructure.Bridge;

public static class BridgeContractMapper
{
    public static QueryResponse<SessionStateContract> ToResponse(QueryResult<SessionState> result) =>
        ToQueryResponse(result, static snapshot => new SessionStateContract(
            snapshot.PipeName,
            snapshot.IsBridgeServerRunning,
            snapshot.ReadyComponentCount,
            snapshot.TotalComponentCount,
            snapshot.Components
                .Select(static component => new SessionComponentContract(component.ComponentName, component.IsReady, component.Status))
                .ToArray(),
            snapshot.SummaryText));

    public static QueryResponse<PlayerContextContract> ToResponse(QueryResult<PlayerContextSnapshot> result) =>
        ToQueryResponse(result, static snapshot => new PlayerContextContract(
            snapshot.CharacterName,
            snapshot.HomeWorld,
            snapshot.CurrentWorld,
            snapshot.ClassJobId,
            snapshot.ClassJobName,
            snapshot.Level,
            snapshot.TerritoryId,
            snapshot.TerritoryName,
            snapshot.MapId,
            snapshot.MapName,
            ToContract(snapshot.Position),
            snapshot.InCombat,
            snapshot.InDuty,
            snapshot.IsCrafting,
            snapshot.IsGathering,
            snapshot.IsMounted,
            snapshot.IsMoving,
            snapshot.ZoneType,
            snapshot.ContentStatus,
            snapshot.SummaryText));

    public static QueryResponse<DutyContextContract> ToResponse(QueryResult<DutyContextSnapshot> result) =>
        ToQueryResponse(result, static snapshot => new DutyContextContract(
            snapshot.TerritoryId,
            snapshot.DutyName,
            snapshot.DutyType,
            snapshot.InDuty,
            snapshot.IsDutyComplete,
            snapshot.SummaryText));

    public static QueryResponse<InventorySummaryContract> ToResponse(QueryResult<InventorySummarySnapshot> result) =>
        ToQueryResponse(result, static snapshot => new InventorySummaryContract(
            snapshot.CurrencyGil,
            snapshot.OccupiedSlots,
            snapshot.TotalSlots,
            snapshot.CategoryCounts,
            snapshot.SummaryText));

    public static QueryResponse<IReadOnlyList<AddonSummaryContract>> ToResponse(QueryResult<IReadOnlyList<AddonSummary>> result) =>
        ToQueryResponse<IReadOnlyList<AddonSummary>, IReadOnlyList<AddonSummaryContract>>(
            result,
            static snapshots => snapshots
                .Select(static snapshot => new AddonSummaryContract(
                    snapshot.AddonName,
                    snapshot.IsReady,
                    snapshot.IsVisible,
                    snapshot.CapturedAt,
                    snapshot.SummaryText))
                .ToArray());

    public static QueryResponse<AddonTreeContract> ToResponse(QueryResult<AddonTreeSnapshot> result) =>
        ToQueryResponse(result, static snapshot => new AddonTreeContract(
            snapshot.AddonName,
            snapshot.CapturedAt,
            snapshot.Roots.Select(ToContract).ToArray()));

    public static QueryResponse<StringTableContract> ToResponse(QueryResult<StringTableSnapshot> result) =>
        ToQueryResponse(result, static snapshot => new StringTableContract(
            snapshot.AddonName,
            snapshot.CapturedAt,
            snapshot.Entries.Select(static entry => new StringTableEntryContract(entry.Index, entry.RawValue, entry.DecodedValue)).ToArray()));

    public static CapabilityStateResponse ToResponse(ExposurePolicy policy) =>
        new(
            ContractVersion.Current,
            policy.EnabledTools.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            policy.EnabledResources.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            policy.EnabledAddons.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            policy.ObservationProfileEnabled,
            policy.ActionProfileEnabled);

    private static QueryResponse<TContract> ToQueryResponse<TSnapshot, TContract>(
        QueryResult<TSnapshot> result,
        Func<TSnapshot, TContract> map)
        where TSnapshot : class
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return new QueryResponse<TContract>(
            Available: result.IsSuccess,
            Reason: result.Reason,
            ContractVersion: ContractVersion.Current,
            CapturedAt: result.CapturedAt,
            SnapshotAgeMs: result.SnapshotAgeMs,
            Data: result.IsSuccess && result.Value is not null ? map(result.Value) : null);
    }

    private static PositionContract? ToContract(PositionSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new PositionContract(snapshot.X, snapshot.Y, snapshot.Z, snapshot.Precision);

    private static NodeContract ToContract(NodeSnapshot snapshot) =>
        new(
            snapshot.NodeId,
            snapshot.NodeType,
            snapshot.Visible,
            snapshot.X,
            snapshot.Y,
            snapshot.Width,
            snapshot.Height,
            snapshot.Text,
            snapshot.Children.Select(ToContract).ToArray());
}
