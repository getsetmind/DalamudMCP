using System.Globalization;
using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "nearby.interactables",
    Description = "Gets nearby interactable objects.",
    Summary = "Gets nearby interactables.")]
[ResultFormatter(typeof(NearbyInteractablesOperation.TextFormatter))]
[CliCommand("nearby", "interactables")]
[McpTool("get_nearby_interactables")]
public sealed partial class NearbyInteractablesOperation
    : IOperation<NearbyInteractablesOperation.Request, NearbyInteractablesSnapshot>, IPluginReaderStatus
{
    private readonly Func<Request, CancellationToken, ValueTask<NearbyInteractablesSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public NearbyInteractablesOperation(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);

        executor = CreateDalamudExecutor(framework, clientState, objectTable);
        isReadyProvider = () => clientState.IsLoggedIn && objectTable.LocalPlayer is not null;
        detailProvider = () => isReadyProvider() ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal NearbyInteractablesOperation(
        Func<Request, CancellationToken, ValueTask<NearbyInteractablesSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "nearby.interactables";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<NearbyInteractablesSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("nearby.interactables")]
    [LegacyBridgeRequest("GetNearbyInteractables")]
    public sealed partial class Request
    {
        [Option("max-distance", Description = "Maximum search radius in yalms.", Required = false)]
        public double? MaxDistance { get; init; }

        [Option("name-contains", Description = "Substring used to filter nearby interactables.", Required = false)]
        public string? NameContains { get; init; }

        [Option("include-players", Description = "Include players in the nearby search.", Required = false)]
        public bool? IncludePlayers { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<NearbyInteractablesSnapshot>
    {
        public string? FormatText(NearbyInteractablesSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<NearbyInteractablesSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            double maxDistance = NormalizeMaxDistance(request.MaxDistance);
            string? nameContains = NormalizeNameFilter(request.NameContains);
            bool includePlayers = request.IncludePlayers ?? false;

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, objectTable, maxDistance, nameContains, includePlayers, cancellationToken);

            return await framework.RunOnFrameworkThread(
                    () => ReadCurrentCore(clientState, objectTable, maxDistance, nameContains, includePlayers, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static NearbyInteractablesSnapshot ReadCurrentCore(
        IClientState clientState,
        IObjectTable objectTable,
        double maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IPlayerCharacter? localPlayer = objectTable.LocalPlayer;
        if (!clientState.IsLoggedIn || localPlayer is null)
            throw new InvalidOperationException("Nearby interactables are not available because the local player is not ready.");

        NearbyInteractable[] interactables = objectTable
            .Where(static gameObject => gameObject is not null)
            .Select(static gameObject => gameObject!)
            .Where(gameObject => gameObject.GameObjectId != localPlayer.GameObjectId)
            .Where(static gameObject => gameObject.IsTargetable)
            .Where(gameObject => includePlayers || !string.Equals(gameObject.ObjectKind.ToString(), "Player", StringComparison.OrdinalIgnoreCase))
            .Where(gameObject => MatchesName(gameObject.Name.TextValue, nameContains))
            .Select(gameObject => CreateInteractable(localPlayer.Position, gameObject))
            .Where(interactable => interactable.Distance <= maxDistance)
            .OrderBy(static interactable => interactable.Distance)
            .Take(32)
            .ToArray();

        return new NearbyInteractablesSnapshot(
            DateTimeOffset.UtcNow,
            maxDistance,
            interactables,
            $"{interactables.Length.ToString(CultureInfo.InvariantCulture)} interactable objects within {maxDistance.ToString("0.#", CultureInfo.InvariantCulture)} yalms.");
    }

    private static bool MatchesName(string? objectName, string? nameContains)
    {
        if (string.IsNullOrWhiteSpace(nameContains))
            return true;

        return !string.IsNullOrWhiteSpace(objectName)
               && objectName.Contains(nameContains, StringComparison.OrdinalIgnoreCase);
    }

    private static NearbyInteractable CreateInteractable(Vector3 localPosition, IGameObject gameObject)
    {
        Vector3 position = gameObject.Position;
        double distance = Math.Round(Vector3.Distance(localPosition, position), 1);
        string objectName = string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
        return new NearbyInteractable(
            $"0x{gameObject.GameObjectId:X}",
            objectName,
            gameObject.ObjectKind.ToString(),
            gameObject.IsTargetable,
            distance,
            Math.Round(gameObject.HitboxRadius, 1),
            new NearbyInteractablePosition(
                Math.Round(position.X, 1),
                Math.Round(position.Y, 1),
                Math.Round(position.Z, 1)));
    }

    private static double NormalizeMaxDistance(double? maxDistance)
    {
        if (maxDistance is null)
            return 8d;

        if (double.IsNaN(maxDistance.Value) || double.IsInfinity(maxDistance.Value))
            throw new ArgumentOutOfRangeException(nameof(maxDistance), "Max distance must be finite.");

        return Math.Clamp(maxDistance.Value, 1d, 40d);
    }

    private static string? NormalizeNameFilter(string? nameContains)
    {
        return string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
    }
}

[MemoryPackable]
public sealed partial record NearbyInteractablePosition(
    double X,
    double Y,
    double Z);

[MemoryPackable]
public sealed partial record NearbyInteractable(
    string GameObjectId,
    string Name,
    string ObjectKind,
    bool IsTargetable,
    double Distance,
    double HitboxRadius,
    NearbyInteractablePosition? Position);

[MemoryPackable]
public sealed partial record NearbyInteractablesSnapshot(
    DateTimeOffset CapturedAt,
    double MaxDistance,
    NearbyInteractable[] Interactables,
    string SummaryText);
