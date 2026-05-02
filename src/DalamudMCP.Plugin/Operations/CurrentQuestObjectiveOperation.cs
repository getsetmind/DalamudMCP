using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using MemoryPack;
using AgentMap = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap;
using GameUiMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;
using MapMarkerData = FFXIVClientStructs.FFXIV.Client.Game.UI.MapMarkerData;
using MarkerInfo = FFXIVClientStructs.FFXIV.Client.Game.UI.MarkerInfo;
using QuestLinkMarker = FFXIVClientStructs.FFXIV.Client.UI.Agent.QuestLinkMarker;
using QuestManager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "quest.current-objective",
    Description = "Gets the currently tracked quest objective.",
    Summary = "Gets the current quest objective.")]
[ResultFormatter(typeof(CurrentQuestObjectiveOperation.TextFormatter))]
[CliCommand("quest", "current-objective")]
[McpTool("get_current_quest_objective")]
public sealed partial class CurrentQuestObjectiveOperation
    : IOperation<CurrentQuestObjectiveOperation.Request, CurrentQuestObjectiveSnapshot>, IPluginReaderStatus
{
    private const byte NormalQuestType = 1;
    private const byte LeveQuestType = 2;

    private static readonly ClientLanguage[] SearchLanguages =
    [
        ClientLanguage.English,
        ClientLanguage.Japanese,
        ClientLanguage.German,
        ClientLanguage.French
    ];

    private readonly Func<CancellationToken, ValueTask<CurrentQuestObjectiveSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public CurrentQuestObjectiveOperation(
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(dataManager);

        executor = CreateDalamudExecutor(framework, clientState, dataManager);
        isReadyProvider = () => clientState.IsLoggedIn;
        detailProvider = () => clientState.IsLoggedIn ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal CurrentQuestObjectiveOperation(
        Func<CancellationToken, ValueTask<CurrentQuestObjectiveSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "quest.current-objective";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<CurrentQuestObjectiveSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("quest.current-objective")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<CurrentQuestObjectiveSnapshot>
    {
        public string? FormatText(CurrentQuestObjectiveSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<CancellationToken, ValueTask<CurrentQuestObjectiveSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        return async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, dataManager, cancellationToken);

            return await framework.RunOnFrameworkThread(() =>
                    ReadCurrentCore(clientState, dataManager, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe CurrentQuestObjectiveSnapshot ReadCurrentCore(
        IClientState clientState,
        IDataManager dataManager,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Current quest objective is not available because the local player is not logged in.");

        QuestManager* questManager = QuestManager.Instance();
        GameUiMap* map = GameUiMap.Instance();
        AgentMap* agentMap = AgentMap.Instance();
        if (questManager is null || map is null || agentMap is null)
            throw new InvalidOperationException("Quest objective systems are not available.");

        ushort territoryType = checked((ushort)clientState.TerritoryType);
        TrackedQuestContext? trackedQuest = ResolveTrackedQuest(questManager, clientState, dataManager);
        if (trackedQuest is null)
        {
            return new CurrentQuestObjectiveSnapshot(
                DateTimeOffset.UtcNow,
                territoryType,
                null,
                null,
                null,
                null,
                [],
                [],
                $"No tracked quest objective is currently available in Territory#{clientState.TerritoryType.ToString(CultureInfo.InvariantCulture)}.");
        }

        CurrentQuestObjectiveVisibleMarker[] visibleMarkers = ResolveVisibleMarkers(map, trackedQuest.QuestId);
        CurrentQuestObjectiveLinkMarker[] linkMarkers = ResolveLinkMarkers(agentMap, trackedQuest.QuestId);
        TrackedQuestContext effectiveTrackedQuest = trackedQuest with
        {
            QuestName = ResolveQuestDisplayName(trackedQuest.QuestName, visibleMarkers, linkMarkers)
        };
        string summaryText = BuildSummary(effectiveTrackedQuest, territoryType, visibleMarkers.Length, linkMarkers.Length);

        return new CurrentQuestObjectiveSnapshot(
            DateTimeOffset.UtcNow,
            territoryType,
            effectiveTrackedQuest.QuestId,
            effectiveTrackedQuest.QuestName,
            effectiveTrackedQuest.QuestKind,
            effectiveTrackedQuest.Sequence,
            visibleMarkers,
            linkMarkers,
            summaryText);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe TrackedQuestContext? ResolveTrackedQuest(
        QuestManager* questManager,
        IClientState clientState,
        IDataManager dataManager)
    {
        foreach (TrackingWork trackedQuest in questManager->TrackedQuests)
        {
            TrackedQuestContext? resolved = ResolveTrackedQuest(questManager, trackedQuest, clientState, dataManager);
            if (resolved is not null)
                return resolved;
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe TrackedQuestContext? ResolveTrackedQuest(
        QuestManager* questManager,
        TrackingWork trackedQuest,
        IClientState clientState,
        IDataManager dataManager)
    {
        return trackedQuest.QuestType switch
        {
            NormalQuestType => ResolveTrackedNormalQuest(questManager, trackedQuest.Index, clientState, dataManager),
            LeveQuestType => ResolveTrackedLeveQuest(questManager, trackedQuest.Index),
            _ => null
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe TrackedQuestContext? ResolveTrackedNormalQuest(
        QuestManager* questManager,
        byte trackedIndex,
        IClientState clientState,
        IDataManager dataManager)
    {
        int questIndex = trackedIndex;
        if (questIndex >= questManager->NormalQuests.Length)
            return null;

        QuestWork questWork = questManager->NormalQuests[questIndex];
        if (questWork.QuestId == 0)
            return null;

        Quest? quest = ResolveQuestRow(clientState, dataManager, questWork.QuestId);
        string questName = quest is Quest row
            ? ReadQuestName(row)
            : $"Quest#{questWork.QuestId.ToString(CultureInfo.InvariantCulture)}";

        return new TrackedQuestContext(
            questWork.QuestId,
            questName,
            "normal",
            questWork.Sequence);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe TrackedQuestContext? ResolveTrackedLeveQuest(
        QuestManager* questManager,
        byte trackedIndex)
    {
        int leveIndex = trackedIndex;
        if (leveIndex >= questManager->LeveQuests.Length)
            return null;

        LeveWork leveWork = questManager->LeveQuests[leveIndex];
        if (leveWork.LeveId == 0)
            return null;

        return new TrackedQuestContext(
            leveWork.LeveId,
            $"Leve#{leveWork.LeveId.ToString(CultureInfo.InvariantCulture)}",
            "leve",
            leveWork.Sequence);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe CurrentQuestObjectiveVisibleMarker[] ResolveVisibleMarkers(GameUiMap* map, uint questId)
    {
        List<CurrentQuestObjectiveVisibleMarker> markers = [];
        HashSet<string> seenMarkerKeys = [];

        foreach (MarkerInfo marker in map->QuestMarkers)
        {
            if (!marker.ShouldRender || !MarkerMatchesQuest(marker, questId))
                continue;

            if (marker.MarkerData.Count <= 0)
            {
                CurrentQuestObjectiveVisibleMarker fallbackMarker = new(
                    marker.ObjectiveId == 0 ? null : marker.ObjectiveId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    NormalizeRecommendedLevel(marker.RecommendedLevel),
                    null,
                    null,
                    ReadMarkerLabel(marker));
                if (seenMarkerKeys.Add(CreateVisibleMarkerKey(fallbackMarker)))
                    markers.Add(fallbackMarker);

                continue;
            }

            foreach (MapMarkerData markerData in marker.MarkerData)
            {
                if (!MarkerDataMatchesQuest(markerData, marker.ObjectiveId, questId))
                    continue;

                CurrentQuestObjectiveVisibleMarker visibleMarker = CreateVisibleMarker(marker, markerData);
                if (seenMarkerKeys.Add(CreateVisibleMarkerKey(visibleMarker)))
                    markers.Add(visibleMarker);
            }
        }

        return markers.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static unsafe CurrentQuestObjectiveLinkMarker[] ResolveLinkMarkers(AgentMap* agentMap, uint questId)
    {
        List<CurrentQuestObjectiveLinkMarker> markers = [];
        HashSet<string> seenMarkerKeys = [];

        AddQuestLinkMarkers(markers, seenMarkerKeys, agentMap->MapQuestLinkContainer.Markers, questId, "map");
        AddQuestLinkMarkers(markers, seenMarkerKeys, agentMap->MiniMapQuestLinkContainer.Markers, questId, "minimap");

        return markers.ToArray();
    }

    private static void AddQuestLinkMarkers(
        List<CurrentQuestObjectiveLinkMarker> markers,
        HashSet<string> seenMarkerKeys,
        Span<QuestLinkMarker> questLinkMarkers,
        uint questId,
        string scope)
    {
        foreach (QuestLinkMarker marker in questLinkMarkers)
        {
            if (marker.Valid == 0 || marker.QuestId != questId)
                continue;

            CurrentQuestObjectiveLinkMarker linkMarker = new(
                scope,
                marker.QuestId,
                ReadUtf8String(marker.TooltipText.ToString()),
                NormalizeInt(marker.RecommendedLevel),
                NormalizeUInt(marker.IconId),
                NormalizeUInt(marker.LevelId),
                NormalizeUInt(marker.SourceMapId),
                NormalizeUInt(marker.TargetMapId));

            if (seenMarkerKeys.Add(CreateLinkMarkerKey(linkMarker)))
                markers.Add(linkMarker);
        }
    }

    private static bool MarkerMatchesQuest(MarkerInfo marker, uint questId)
    {
        if (marker.ObjectiveId == questId)
            return true;

        foreach (MapMarkerData markerData in marker.MarkerData)
            if (MarkerDataMatchesQuest(markerData, marker.ObjectiveId, questId))
                return true;

        return false;
    }

    private static bool MarkerDataMatchesQuest(MapMarkerData markerData, uint markerObjectiveId, uint questId)
    {
        return markerData.ObjectiveId == questId || markerObjectiveId == questId;
    }

    private static CurrentQuestObjectiveVisibleMarker CreateVisibleMarker(MarkerInfo marker, MapMarkerData markerData)
    {
        return new CurrentQuestObjectiveVisibleMarker(
            markerData.ObjectiveId == 0 ? NormalizeUInt(marker.ObjectiveId) : markerData.ObjectiveId,
            NormalizeUInt(markerData.DataId),
            NormalizeUInt(markerData.MapId),
            NormalizeUShort(markerData.TerritoryTypeId),
            NormalizeUInt(markerData.PlaceNameZoneId),
            NormalizeUInt(markerData.PlaceNameId),
            NormalizeRecommendedLevel(markerData.RecommendedLevel == 0 ? marker.RecommendedLevel : markerData.RecommendedLevel),
            markerData.Radius <= 0 ? null : Math.Round(markerData.Radius, 1),
            new CurrentQuestObjectivePosition(
                Math.Round(markerData.Position.X, 1),
                Math.Round(markerData.Position.Y, 1),
                Math.Round(markerData.Position.Z, 1)),
            ReadMarkerLabel(marker));
    }

    [SupportedOSPlatform("windows")]
    private static Quest? ResolveQuestRow(IClientState clientState, IDataManager dataManager, uint questId)
    {
        foreach (ClientLanguage? language in EnumerateLanguages(clientState))
        {
            ExcelSheet<Quest>? sheet = dataManager.GetExcelSheet<Quest>(language);
            if (sheet is null)
                continue;

            foreach (Quest row in sheet)
                if (row.RowId == questId)
                    return row;
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<ClientLanguage?> EnumerateLanguages(IClientState clientState)
    {
        yield return clientState.ClientLanguage;
        foreach (ClientLanguage language in SearchLanguages)
            if (language != clientState.ClientLanguage)
                yield return language;
    }

    private static string ReadQuestName(Quest row)
    {
        return TryReadStringProperty(row, "Name")
               ?? TryReadStringProperty(row, "NameEnglish")
               ?? $"Quest#{row.RowId.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string? TryReadStringProperty(object row, string propertyName)
    {
        PropertyInfo? property = row.GetType().GetProperty(propertyName);
        object? value = property?.GetValue(row);
        string? text = value?.ToString();
        return ReadUtf8String(text);
    }

    private static string? ReadUtf8String(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ReadMarkerLabel(MarkerInfo marker)
    {
        return ReadUtf8String(marker.Label.ToString());
    }

    private static uint? NormalizeUInt(uint value)
    {
        return value == 0 ? null : value;
    }

    private static ushort? NormalizeUShort(ushort value)
    {
        return value == 0 ? null : value;
    }

    private static int? NormalizeInt(int value)
    {
        return value <= 0 ? null : value;
    }

    private static int? NormalizeRecommendedLevel(ushort value)
    {
        return value == 0 ? null : value;
    }

    private static string CreateVisibleMarkerKey(CurrentQuestObjectiveVisibleMarker marker)
    {
        return FormattableString.Invariant(
            $"{marker.ObjectiveId}|{marker.IssuerDataId}|{marker.MapId}|{marker.TerritoryTypeId}|{marker.Position?.X}|{marker.Position?.Y}|{marker.Position?.Z}|{marker.Label}");
    }

    private static string CreateLinkMarkerKey(CurrentQuestObjectiveLinkMarker marker)
    {
        return FormattableString.Invariant(
            $"{marker.Scope}|{marker.QuestId}|{marker.LevelId}|{marker.SourceMapId}|{marker.TargetMapId}|{marker.TooltipText}");
    }

    private static string BuildSummary(
        TrackedQuestContext trackedQuest,
        ushort territoryTypeId,
        int visibleMarkerCount,
        int linkMarkerCount)
    {
        string prefix = $"{trackedQuest.QuestName} ({trackedQuest.QuestId.ToString(CultureInfo.InvariantCulture)}) is tracked at sequence {trackedQuest.Sequence.ToString(CultureInfo.InvariantCulture)}";
        if (visibleMarkerCount > 0)
            return $"{prefix}; {visibleMarkerCount.ToString(CultureInfo.InvariantCulture)} visible objective markers found in Territory#{territoryTypeId.ToString(CultureInfo.InvariantCulture)}.";

        if (linkMarkerCount > 0)
            return $"{prefix}; {linkMarkerCount.ToString(CultureInfo.InvariantCulture)} quest link markers available.";

        return $"{prefix}; no visible objective markers were found in Territory#{territoryTypeId.ToString(CultureInfo.InvariantCulture)}.";
    }

    private static string ResolveQuestDisplayName(
        string fallbackQuestName,
        IReadOnlyList<CurrentQuestObjectiveVisibleMarker> visibleMarkers,
        IReadOnlyList<CurrentQuestObjectiveLinkMarker> linkMarkers)
    {
        if (!string.IsNullOrWhiteSpace(fallbackQuestName) && !fallbackQuestName.StartsWith("Quest#", StringComparison.Ordinal))
            return fallbackQuestName;

        string? markerLabel = visibleMarkers
            .Select(static marker => marker.Label)
            .FirstOrDefault(static label => !string.IsNullOrWhiteSpace(label));
        if (!string.IsNullOrWhiteSpace(markerLabel))
            return markerLabel;

        string? tooltipText = linkMarkers
            .Select(static marker => marker.TooltipText)
            .FirstOrDefault(static text => !string.IsNullOrWhiteSpace(text));
        return string.IsNullOrWhiteSpace(tooltipText) ? fallbackQuestName : tooltipText;
    }

    private sealed record TrackedQuestContext(
        uint QuestId,
        string QuestName,
        string QuestKind,
        byte Sequence);
}

[MemoryPackable]
public sealed partial record CurrentQuestObjectivePosition(
    double X,
    double Y,
    double Z);

[MemoryPackable]
public sealed partial record CurrentQuestObjectiveVisibleMarker(
    uint? ObjectiveId,
    uint? IssuerDataId,
    uint? MapId,
    ushort? TerritoryTypeId,
    uint? PlaceNameZoneId,
    uint? PlaceNameId,
    int? RecommendedLevel,
    double? Radius,
    CurrentQuestObjectivePosition? Position,
    string? Label);

[MemoryPackable]
public sealed partial record CurrentQuestObjectiveLinkMarker(
    string Scope,
    uint QuestId,
    string? TooltipText,
    int? RecommendedLevel,
    uint? IconId,
    uint? LevelId,
    uint? SourceMapId,
    uint? TargetMapId);

[MemoryPackable]
public sealed partial record CurrentQuestObjectiveSnapshot(
    DateTimeOffset CapturedAt,
    ushort CurrentTerritoryTypeId,
    uint? QuestId,
    string? QuestName,
    string? QuestKind,
    byte? Sequence,
    CurrentQuestObjectiveVisibleMarker[] VisibleMarkers,
    CurrentQuestObjectiveLinkMarker[] LinkMarkers,
    string SummaryText);



