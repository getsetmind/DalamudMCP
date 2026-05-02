using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "player.context",
    Description = "Gets the current player context.",
    Summary = "Gets player context.")]
[ResultFormatter(typeof(PlayerContextOperation.TextFormatter))]
[CliCommand("player", "context")]
[McpTool("get_player_context")]
public sealed partial class PlayerContextOperation
    : IOperation<PlayerContextOperation.Request, PlayerContextSnapshot>, IPluginReaderStatus
{
    private readonly Func<CancellationToken, ValueTask<PlayerContextSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public PlayerContextOperation(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        IPlayerState playerState)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(playerState);

        executor = CreateDalamudExecutor(framework, clientState, objectTable, playerState);
        isReadyProvider = () => clientState.IsLoggedIn && playerState.IsLoaded && objectTable.LocalPlayer is not null;
        detailProvider = () => isReadyProvider() ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal PlayerContextOperation(
        Func<CancellationToken, ValueTask<PlayerContextSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "player.context";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<PlayerContextSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("player.context")]
    [LegacyBridgeRequest("GetPlayerContext")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<PlayerContextSnapshot>
    {
        public string? FormatText(PlayerContextSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return $"{result.CharacterName} @ {result.HomeWorld} ({result.JobName} {result.JobLevel})";
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<CancellationToken, ValueTask<PlayerContextSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        IPlayerState playerState)
    {
        return async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, objectTable, playerState, cancellationToken);

            return await framework.RunOnFrameworkThread(() =>
                    ReadCurrentCore(clientState, objectTable, playerState, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static PlayerContextSnapshot ReadCurrentCore(
        IClientState clientState,
        IObjectTable objectTable,
        IPlayerState playerState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IPlayerCharacter? player = objectTable.LocalPlayer;
        if (!clientState.IsLoggedIn || !playerState.IsLoaded || player is null)
            throw new InvalidOperationException("Player context is not available because the local player is not ready.");

        string characterName = string.IsNullOrWhiteSpace(playerState.CharacterName)
            ? "Unknown"
            : playerState.CharacterName;
        string homeWorld = ResolveWorldName(playerState.HomeWorld);
        string jobName = ResolveClassJobName(playerState.ClassJob);
        int jobLevel = ConvertToNullableInt(playerState.Level) ?? 0;
        int? territoryId = ConvertToNullableInt(clientState.TerritoryType);
        string territoryName = territoryId is null
            ? "Unknown"
            : $"Territory#{territoryId.Value.ToString(CultureInfo.InvariantCulture)}";
        PlayerPosition position = new(
            Math.Round(player.Position.X, 1),
            Math.Round(player.Position.Y, 1),
            Math.Round(player.Position.Z, 1));

        return new PlayerContextSnapshot(
            characterName,
            homeWorld,
            jobName,
            jobLevel,
            territoryName,
            position);
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

    [SupportedOSPlatform("windows")]
    private static string ResolveWorldName(Lumina.Excel.RowRef<Lumina.Excel.Sheets.World> world)
    {
        string? name = world.ValueNullable?.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? $"World#{world.RowId}" : name;
    }

    [SupportedOSPlatform("windows")]
    private static string ResolveClassJobName(Lumina.Excel.RowRef<Lumina.Excel.Sheets.ClassJob> classJob)
    {
        Lumina.Excel.Sheets.ClassJob? row = classJob.ValueNullable;
        string? name = row?.NameEnglish.ToString();
        if (string.IsNullOrWhiteSpace(name))
            name = row?.Name.ToString();

        return string.IsNullOrWhiteSpace(name) ? $"ClassJob#{classJob.RowId}" : name;
    }
}

[MemoryPackable]
public sealed partial record PlayerPosition(
    double X,
    double Y,
    double Z);

[MemoryPackable]
public sealed partial record PlayerContextSnapshot(
    string CharacterName,
    string HomeWorld,
    string JobName,
    int JobLevel,
    string TerritoryName,
    PlayerPosition Position);



