using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "move.to.nearby.interactable",
    Description = "Starts movement toward a nearby interactable selected by name.",
    Summary = "Moves to a nearby interactable.")]
[ResultFormatter(typeof(MoveToNearbyInteractableOperation.TextFormatter))]
[CliCommand("move", "to", "nearby", "interactable")]
[McpTool("move_to_nearby_interactable")]
public sealed partial class MoveToNearbyInteractableOperation : IOperation<MoveToNearbyInteractableOperation.Request, MoveToEntityResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<MoveToEntityResult>> executor;

    [SupportedOSPlatform("windows")]
    public MoveToNearbyInteractableOperation(
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

    internal MoveToNearbyInteractableOperation(Func<Request, CancellationToken, ValueTask<MoveToEntityResult>> executor)
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
    [ProtocolOperation("move.to.nearby.interactable")]
    [LegacyBridgeRequest("MoveToNearbyInteractable")]
    public sealed partial class Request
    {
        [Option("name-contains", Description = "Substring used to select the nearby interactable.")]
        public string NameContains { get; init; } = string.Empty;

        [Option("max-distance", Description = "Maximum search radius in yalms.", Required = false)]
        public double? MaxDistance { get; init; }

        [Option("allow-flight", Description = "Allow flight when available.", Required = false)]
        public bool? AllowFlight { get; init; }

        [Option("include-players", Description = "Include players in the nearby search.", Required = false)]
        public bool? IncludePlayers { get; init; }
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
            string nameContains = NormalizeNameContains(request.NameContains);
            double maxDistance = request.MaxDistance is null ? 8d : Math.Clamp(request.MaxDistance.Value, 1d, 40d);
            bool includePlayers = request.IncludePlayers ?? false;
            bool allowFlight = request.AllowFlight ?? false;

            if (framework.IsInFrameworkUpdateThread)
            {
                return MoveToNearbyCore(
                    clientState,
                    objectTable,
                    vnavmesh,
                    nameContains,
                    maxDistance,
                    includePlayers,
                    allowFlight,
                    cancellationToken);
            }

            return await framework.RunOnFrameworkThread(
                    () => MoveToNearbyCore(
                        clientState,
                        objectTable,
                        vnavmesh,
                        nameContains,
                        maxDistance,
                        includePlayers,
                        allowFlight,
                        cancellationToken))
                .ConfigureAwait(false);
        };
    }

    private static MoveToEntityResult MoveToNearbyCore(
        IClientState clientState,
        IObjectTable objectTable,
        IVnavmeshIpcClient vnavmesh,
        string nameContains,
        double maxDistance,
        bool includePlayers,
        bool allowFlight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IPlayerCharacter? localPlayer = objectTable.LocalPlayer;
        if (!clientState.IsLoggedIn || localPlayer is null)
            return new MoveToEntityResult(nameContains, false, "not_logged_in", null, null, null, null, "Movement is unavailable before the character is fully logged in.");

        IGameObject? target = objectTable
            .Where(static gameObject => gameObject is not null)
            .Select(static gameObject => gameObject!)
            .Where(gameObject => gameObject.GameObjectId != localPlayer.GameObjectId)
            .Where(static gameObject => gameObject.IsTargetable)
            .Where(gameObject => includePlayers || !string.Equals(gameObject.ObjectKind.ToString(), "Player", StringComparison.OrdinalIgnoreCase))
            .Where(gameObject => !string.IsNullOrWhiteSpace(gameObject.Name.TextValue) && gameObject.Name.TextValue.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            .OrderBy(gameObject => Vector3.Distance(localPlayer.Position, gameObject.Position))
            .FirstOrDefault(gameObject => Vector3.Distance(localPlayer.Position, gameObject.Position) <= maxDistance);

        if (target is null)
            return new MoveToEntityResult(nameContains, false, "object_not_found", null, null, null, null, $"No nearby interactable matched '{nameContains}'.");

        Vector3 destination = vnavmesh.ResolvePointOnFloor(target.Position);
        VnavmeshPathfindAttempt pathfindAttempt = vnavmesh.TryStartPathfind(destination, allowFlight);
        if (!pathfindAttempt.Available)
            return new MoveToEntityResult(nameContains, false, "vnavmesh_ipc_missing", $"0x{target.GameObjectId:X}", ReadName(target), target.ObjectKind.ToString(), null, $"Failed to start movement toward {ReadName(target)}.");

        return new MoveToEntityResult(
            nameContains,
            pathfindAttempt.Succeeded,
            pathfindAttempt.Succeeded ? null : pathfindAttempt.Reason ?? "pathfind_start_failed",
            $"0x{target.GameObjectId:X}",
            ReadName(target),
            target.ObjectKind.ToString(),
            new MoveDestination(Math.Round(destination.X, 1), Math.Round(destination.Y, 1), Math.Round(destination.Z, 1)),
            pathfindAttempt.Succeeded
                ? $"Movement started toward {ReadName(target)}."
                : $"Failed to start movement toward {ReadName(target)}.");
    }

    private static string NormalizeNameContains(string nameContains)
    {
        return string.IsNullOrWhiteSpace(nameContains)
            ? throw new ArgumentException("name-contains is required.", nameof(nameContains))
            : nameContains.Trim();
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
