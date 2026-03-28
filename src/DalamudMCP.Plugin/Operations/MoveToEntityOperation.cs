using System.Globalization;
using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "move.to.entity",
    Description = "Starts movement toward a targetable entity.",
    Summary = "Moves to an entity.")]
[ResultFormatter(typeof(MoveToEntityOperation.TextFormatter))]
[CliCommand("move", "to", "entity")]
[McpTool("move_to_entity")]
public sealed partial class MoveToEntityOperation : IOperation<MoveToEntityOperation.Request, MoveToEntityResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<MoveToEntityResult>> executor;

    [SupportedOSPlatform("windows")]
    public MoveToEntityOperation(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);

        executor = CreateDalamudExecutor(new VnavmeshIpcClient(pluginInterface), framework, clientState, objectTable);
    }

    internal MoveToEntityOperation(Func<Request, CancellationToken, ValueTask<MoveToEntityResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<MoveToEntityResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("move.to.entity")]
    [LegacyBridgeRequest("MoveToEntity")]
    public sealed partial class Request
    {
        [Option("game-object-id", Description = "Game object id to move toward.")]
        public string GameObjectId { get; init; } = string.Empty;

        [Option("allow-flight", Description = "Allow flight when available.", Required = false)]
        public bool? AllowFlight { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<MoveToEntityResult>
    {
        public string? FormatText(MoveToEntityResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<MoveToEntityResult>> CreateDalamudExecutor(
        IVnavmeshIpcClient vnavmesh,
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string gameObjectId = NormalizeGameObjectId(request.GameObjectId);
            bool allowFlight = request.AllowFlight ?? false;

            if (framework.IsInFrameworkUpdateThread)
            {
                return MoveToEntityCore(
                    clientState,
                    objectTable,
                    vnavmesh,
                    gameObjectId,
                    allowFlight,
                    cancellationToken);
            }

            return await framework.RunOnFrameworkThread(
                    () => MoveToEntityCore(
                        clientState,
                        objectTable,
                        vnavmesh,
                        gameObjectId,
                        allowFlight,
                        cancellationToken))
                .ConfigureAwait(false);
        };
    }

    private static MoveToEntityResult MoveToEntityCore(
        IClientState clientState,
        IObjectTable objectTable,
        IVnavmeshIpcClient vnavmesh,
        string gameObjectId,
        bool allowFlight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            return CreateFailure(gameObjectId, "not_logged_in", "Movement is unavailable before the character is fully logged in.");

        if (!TryParseGameObjectId(gameObjectId, out ulong parsedId))
            return CreateFailure(gameObjectId, "invalid_object_id", $"'{gameObjectId}' is not a valid game object id.");

        IGameObject? target = objectTable.FirstOrDefault(candidate => candidate is not null && candidate.GameObjectId == parsedId);
        if (target is null)
            return CreateFailure(gameObjectId, "object_not_found", $"Object {gameObjectId} was not found in the current object table.");

        Vector3 destination = vnavmesh.ResolvePointOnFloor(target.Position);
        VnavmeshPathfindAttempt pathfindAttempt = vnavmesh.TryStartPathfind(destination, allowFlight);
        if (!pathfindAttempt.Available)
            return CreateResult(target, gameObjectId, false, "vnavmesh_ipc_missing", null);

        if (!pathfindAttempt.Succeeded)
        {
            return CreateResult(
                target,
                gameObjectId,
                false,
                pathfindAttempt.Reason ?? "pathfind_start_failed",
                new MoveDestination(Math.Round(destination.X, 1), Math.Round(destination.Y, 1), Math.Round(destination.Z, 1)));
        }

        return CreateResult(
            target,
            gameObjectId,
            true,
            null,
            new MoveDestination(Math.Round(destination.X, 1), Math.Round(destination.Y, 1), Math.Round(destination.Z, 1)));
    }

    private static MoveToEntityResult CreateFailure(string requestedGameObjectId, string reason, string summary)
    {
        return new MoveToEntityResult(requestedGameObjectId, false, reason, null, null, null, null, summary);
    }

    private static MoveToEntityResult CreateResult(
        IGameObject target,
        string requestedGameObjectId,
        bool succeeded,
        string? reason,
        MoveDestination? destination)
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

    private static string NormalizeGameObjectId(string gameObjectId)
    {
        return string.IsNullOrWhiteSpace(gameObjectId)
            ? throw new ArgumentException("Game object id is required.", nameof(gameObjectId))
            : gameObjectId.Trim();
    }

    private static bool TryParseGameObjectId(string value, out ulong gameObjectId)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out gameObjectId);

        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out gameObjectId);
    }

    private static string ReadName(IGameObject gameObject)
    {
        return string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
    }

    private interface IVnavmeshIpcClient
    {
        public VnavmeshPathfindAttempt TryStartPathfind(Vector3 destination, bool allowFlight);

        public Vector3 ResolvePointOnFloor(Vector3 rawDestination);
    }

    private sealed class VnavmeshIpcClient : IVnavmeshIpcClient
    {
        private readonly ICallGateSubscriber<bool> isReadySubscriber;
        private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloorSubscriber;
        private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindSubscriber;

        public VnavmeshIpcClient(IDalamudPluginInterface pluginInterface)
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);
            isReadySubscriber = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
            pointOnFloorSubscriber = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
            pathfindSubscriber = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        }

        public VnavmeshPathfindAttempt TryStartPathfind(Vector3 destination, bool allowFlight)
        {
            if (!isReadySubscriber.HasFunction || !pathfindSubscriber.HasFunction)
                return new VnavmeshPathfindAttempt(false, false, "vnavmesh_ipc_missing");

            try
            {
                if (!isReadySubscriber.InvokeFunc())
                    return new VnavmeshPathfindAttempt(true, false, "vnavmesh_not_ready");

                bool started = pathfindSubscriber.InvokeFunc(destination, allowFlight);
                return new VnavmeshPathfindAttempt(true, started, started ? null : "pathfind_start_failed");
            }
            catch
            {
                return new VnavmeshPathfindAttempt(true, false, "vnavmesh_ipc_error");
            }
        }

        public Vector3 ResolvePointOnFloor(Vector3 rawDestination)
        {
            if (!pointOnFloorSubscriber.HasFunction)
                return rawDestination;

            try
            {
                return pointOnFloorSubscriber.InvokeFunc(rawDestination, false, 4f) ?? rawDestination;
            }
            catch
            {
                return rawDestination;
            }
        }
    }

    private readonly record struct VnavmeshPathfindAttempt(
        bool Available,
        bool Succeeded,
        string? Reason);
}

[MemoryPackable]
public sealed partial record MoveDestination(
    double X,
    double Y,
    double Z);

[MemoryPackable]
public sealed partial record MoveToEntityResult(
    string RequestedGameObjectId,
    bool Succeeded,
    string? Reason,
    string? ResolvedGameObjectId,
    string? TargetName,
    string? ObjectKind,
    MoveDestination? Destination,
    string SummaryText);
