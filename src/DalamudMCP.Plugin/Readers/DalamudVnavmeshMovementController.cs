using System.Globalization;
using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudVnavmeshMovementController : IEntityMovementController
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly ICallGateSubscriber<bool> isReadySubscriber;
    private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveToSubscriber;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloorSubscriber;

    public DalamudVnavmeshMovementController(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.isReadySubscriber = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        this.pathfindAndMoveToSubscriber = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        this.pointOnFloorSubscriber = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
    }

    public Task<MoveToEntityResult> MoveToEntityAsync(string gameObjectId, bool allowFlight, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameObjectId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => MoveToEntityCore(gameObjectId, allowFlight, cancellationToken));
        }

        return Task.FromResult(MoveToEntityCore(gameObjectId, allowFlight, cancellationToken));
    }

    private MoveToEntityResult MoveToEntityCore(string gameObjectId, bool allowFlight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryParseGameObjectId(gameObjectId, out var parsedId))
        {
            return new MoveToEntityResult(
                gameObjectId,
                Succeeded: false,
                Reason: "invalid_object_id",
                ResolvedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Destination: null,
                SummaryText: $"'{gameObjectId}' is not a valid game object id.");
        }

        var target = objectTable.FirstOrDefault(candidate => candidate is not null && candidate.GameObjectId == parsedId);
        if (target is null)
        {
            return new MoveToEntityResult(
                gameObjectId,
                Succeeded: false,
                Reason: "object_not_found",
                ResolvedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Destination: null,
                SummaryText: $"Object {gameObjectId} was not found in the current object table.");
        }

        if (!IsVnavmeshReady(out var readinessFailure))
        {
            return CreateResult(target, gameObjectId, false, readinessFailure, null);
        }

        var destination = ResolveDestination(target.Position);
        var started = pathfindAndMoveToSubscriber.InvokeFunc(destination, allowFlight);
        return CreateResult(
            target,
            gameObjectId,
            started,
            started ? null : "pathfind_start_failed",
            new PositionSnapshot(destination.X, destination.Y, destination.Z, "coarse"));
    }

    private bool IsVnavmeshReady(out string? failureReason)
    {
        failureReason = null;
        try
        {
            if (!isReadySubscriber.HasFunction || !pathfindAndMoveToSubscriber.HasFunction)
            {
                failureReason = "vnavmesh_ipc_missing";
                return false;
            }

            if (!isReadySubscriber.InvokeFunc())
            {
                failureReason = "vnavmesh_not_ready";
                return false;
            }

            return true;
        }
        catch
        {
            failureReason = "vnavmesh_ipc_error";
            return false;
        }
    }

    private Vector3 ResolveDestination(Vector3 rawDestination)
    {
        try
        {
            if (pointOnFloorSubscriber.HasFunction)
            {
                var adjusted = pointOnFloorSubscriber.InvokeFunc(rawDestination, false, 4f);
                if (adjusted.HasValue)
                {
                    return adjusted.Value;
                }
            }
        }
        catch
        {
        }

        return rawDestination;
    }

    private static MoveToEntityResult CreateResult(
        IGameObject target,
        string requestedGameObjectId,
        bool succeeded,
        string? reason,
        PositionSnapshot? destination)
    {
        return new MoveToEntityResult(
            requestedGameObjectId,
            succeeded,
            reason,
            $"0x{target.GameObjectId:X}",
            ReadName(target),
            target.ObjectKind.ToString(),
            destination,
            succeeded
                ? $"Movement started toward {ReadName(target)}."
                : $"Failed to start movement toward {ReadName(target)}.");
    }

    private static bool TryParseGameObjectId(string value, out ulong gameObjectId)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out gameObjectId);
        }

        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out gameObjectId);
    }

    private static string ReadName(IGameObject gameObject) =>
        string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
}
