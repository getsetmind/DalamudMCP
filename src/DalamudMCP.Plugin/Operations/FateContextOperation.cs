using System.Globalization;
using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "fate.context",
    Description = "Gets nearby FATE context.",
    Summary = "Gets nearby FATE context.")]
[ResultFormatter(typeof(FateContextOperation.TextFormatter))]
[CliCommand("fate", "context")]
[McpTool("get_fate_context")]
public sealed partial class FateContextOperation
    : IOperation<FateContextOperation.Request, FateContextSnapshot>, IPluginReaderStatus
{
    private readonly Func<Request, CancellationToken, ValueTask<FateContextSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public FateContextOperation(
        IFramework framework,
        IClientState clientState,
        IFateTable fateTable,
        IObjectTable objectTable)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(fateTable);
        ArgumentNullException.ThrowIfNull(objectTable);

        executor = CreateDalamudExecutor(framework, clientState, fateTable, objectTable);
        isReadyProvider = () => clientState.IsLoggedIn && objectTable.LocalPlayer is not null;
        detailProvider = () => isReadyProvider() ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal FateContextOperation(
        Func<Request, CancellationToken, ValueTask<FateContextSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "fate.context";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<FateContextSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("fate.context")]
    [LegacyBridgeRequest("GetFateContext")]
    public sealed partial class Request
    {
        [Option("max-distance", Description = "Maximum search radius in yalms.", Required = false)]
        public double? MaxDistance { get; init; }

        [Option("name-contains", Description = "Substring used to filter nearby FATEs.", Required = false)]
        public string? NameContains { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<FateContextSnapshot>
    {
        public string? FormatText(FateContextSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<FateContextSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IFateTable fateTable,
        IObjectTable objectTable)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            double maxDistance = NormalizeMaxDistance(request.MaxDistance);
            string? nameContains = NormalizeNameFilter(request.NameContains);

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, fateTable, objectTable, maxDistance, nameContains, cancellationToken);

            return await framework.RunOnFrameworkThread(
                    () => ReadCurrentCore(clientState, fateTable, objectTable, maxDistance, nameContains, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static FateContextSnapshot ReadCurrentCore(
        IClientState clientState,
        IFateTable fateTable,
        IObjectTable objectTable,
        double maxDistance,
        string? nameContains,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn || objectTable.LocalPlayer is null)
            throw new InvalidOperationException("FATE context is not available because the local player is not ready.");

        Vector3 localPosition = objectTable.LocalPlayer.Position;
        FateSnapshot[] fates = fateTable
            .Where(static fate => fate is not null)
            .Select(fate => CreateSnapshot(fate, localPosition))
            .Where(fate => fate.Distance <= maxDistance)
            .Where(fate => MatchesName(fate, nameContains))
            .OrderBy(static fate => fate.Distance)
            .Take(16)
            .ToArray();

        int? territoryId = ConvertToNullableInt(clientState.TerritoryType);
        return new FateContextSnapshot(
            DateTimeOffset.UtcNow,
            territoryId,
            maxDistance,
            fates,
            $"{fates.Length.ToString(CultureInfo.InvariantCulture)} FATEs within {maxDistance.ToString("0.#", CultureInfo.InvariantCulture)} yalms.");
    }

    private static FateSnapshot CreateSnapshot(IFate fate, Vector3 localPosition)
    {
        Vector3 position = fate.Position;
        return new FateSnapshot(
            fate.FateId,
            string.IsNullOrWhiteSpace(fate.Name.TextValue) ? $"FATE#{fate.FateId}" : fate.Name.TextValue,
            fate.State.ToString(),
            ConvertToNullableInt(fate.Level),
            ConvertToNullableInt(fate.MaxLevel),
            ConvertToNullableInt(fate.Progress) ?? 0,
            ConvertToNullableInt(fate.TimeRemaining),
            fate.HasBonus,
            Math.Round(Vector3.Distance(localPosition, fate.Position), 1),
            new FatePosition(
                Math.Round(position.X, 1),
                Math.Round(position.Y, 1),
                Math.Round(position.Z, 1)),
            string.IsNullOrWhiteSpace(fate.Objective.TextValue) ? null : fate.Objective.TextValue,
            string.IsNullOrWhiteSpace(fate.Description.TextValue) ? null : fate.Description.TextValue);
    }

    private static bool MatchesName(FateSnapshot snapshot, string? nameContains)
    {
        if (string.IsNullOrWhiteSpace(nameContains))
            return true;

        return snapshot.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
               || (snapshot.Objective?.Contains(nameContains, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static int? ConvertToNullableInt<T>(T value)
        where T : struct
    {
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static double NormalizeMaxDistance(double? maxDistance)
    {
        if (maxDistance is null)
            return 120d;

        if (double.IsNaN(maxDistance.Value) || double.IsInfinity(maxDistance.Value))
            throw new ArgumentOutOfRangeException(nameof(maxDistance), "Max distance must be finite.");

        return Math.Clamp(maxDistance.Value, 5d, 400d);
    }

    private static string? NormalizeNameFilter(string? nameContains)
    {
        return string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
    }
}

[MemoryPackable]
public sealed partial record FatePosition(
    double X,
    double Y,
    double Z);

[MemoryPackable]
public sealed partial record FateSnapshot(
    uint FateId,
    string Name,
    string State,
    int? Level,
    int? MaxLevel,
    int ProgressPercent,
    int? TimeRemainingSeconds,
    bool HasBonus,
    double Distance,
    FatePosition? Position,
    string? Objective,
    string? Description);

[MemoryPackable]
public sealed partial record FateContextSnapshot(
    DateTimeOffset CapturedAt,
    int? TerritoryId,
    double MaxDistance,
    FateSnapshot[] Fates,
    string SummaryText);
