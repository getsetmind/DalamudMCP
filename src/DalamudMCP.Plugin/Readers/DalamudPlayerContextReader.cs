using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;
using Lumina.Excel.Sheets;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudPlayerContextReader : IPlayerContextReader, IPluginReaderDiagnostics
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPlayerState playerState;
    private readonly ICondition condition;
    private readonly IFramework framework;

    public DalamudPlayerContextReader(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        IPlayerState playerState,
        ICondition condition)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(playerState);
        ArgumentNullException.ThrowIfNull(condition);
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.playerState = playerState;
        this.condition = condition;
    }

    public string ComponentName => "player_context";

    public bool IsReady => clientState.IsLoggedIn && playerState.IsLoaded;

    public string Status => IsReady ? "ready" : "not_logged_in";

    public Task<PlayerContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(cancellationToken));
    }

    private PlayerContextSnapshot? ReadCurrentCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn || !playerState.IsLoaded || objectTable.LocalPlayer is null)
        {
            return null;
        }

        var player = objectTable.LocalPlayer;
        var territoryId = ConvertToNullableInt(clientState.TerritoryType);
        var mapId = ConvertToNullableInt(clientState.MapId);
        var position = player.Position;
        var characterName = string.IsNullOrWhiteSpace(playerState.CharacterName) ? "Unknown" : playerState.CharacterName;
        var classJobName = ResolveClassJobName(playerState.ClassJob);
        var homeWorldName = ResolveWorldName(playerState.HomeWorld);
        var currentWorldName = ResolveWorldName(playerState.CurrentWorld);
        var isInDuty = condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95);
        var isMounted = condition.Any(ConditionFlag.Mounted, ConditionFlag.RidingPillion);
        var isCrafting = condition.Any(ConditionFlag.Crafting, ConditionFlag.ExecutingCraftingAction);
        var isGathering = condition.Any(ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction);

        var snapshot = new PlayerContextSnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            CharacterName: characterName,
            HomeWorld: homeWorldName,
            CurrentWorld: currentWorldName,
            ClassJobId: ConvertToNullableInt(playerState.ClassJob.RowId),
            ClassJobName: classJobName,
            Level: ConvertToNullableInt(playerState.Level),
            TerritoryId: territoryId,
            TerritoryName: territoryId is null ? null : $"Territory#{territoryId.Value}",
            MapId: mapId,
            MapName: mapId is null ? null : $"Map#{mapId.Value}",
            Position: new PositionSnapshot(
                Math.Round(position.X, 1),
                Math.Round(position.Y, 1),
                Math.Round(position.Z, 1),
                "coarse"),
            InCombat: condition[ConditionFlag.InCombat],
            InDuty: isInDuty,
            IsCrafting: isCrafting,
            IsGathering: isGathering,
            IsMounted: isMounted,
            IsMoving: null,
            ZoneType: isInDuty ? "duty" : "world",
            ContentStatus: GetContentStatus(isCrafting, isGathering, isInDuty),
            SummaryText: BuildSummary(characterName, classJobName, territoryId, mapId, isInDuty));

        return snapshot;
    }

    private static int? ConvertToNullableInt<T>(T value)
        where T : struct
    {
        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static string GetContentStatus(bool isCrafting, bool isGathering, bool isInDuty)
    {
        if (isCrafting)
        {
            return "crafting";
        }

        if (isGathering)
        {
            return "gathering";
        }

        return isInDuty ? "in_duty" : "idle";
    }

    private static string BuildSummary(string characterName, string classJobName, int? territoryId, int? mapId, bool isInDuty)
    {
        var location = territoryId is null && mapId is null
            ? "an unknown location"
            : $"territory {territoryId?.ToString(CultureInfo.InvariantCulture) ?? "?"}, map {mapId?.ToString(CultureInfo.InvariantCulture) ?? "?"}";
        var dutyText = isInDuty ? "in duty" : "in the open world";
        return $"{characterName} on {classJobName} at {location} ({dutyText}).";
    }

    private static string ResolveWorldName(Lumina.Excel.RowRef<World> world)
    {
        var name = world.ValueNullable?.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? $"World#{world.RowId}" : name;
    }

    private static string ResolveClassJobName(Lumina.Excel.RowRef<ClassJob> classJob)
    {
        var row = classJob.ValueNullable;
        var name = row?.NameEnglish.ToString();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = row?.Name.ToString();
        }

        return string.IsNullOrWhiteSpace(name) ? $"ClassJob#{classJob.RowId}" : name;
    }
}
