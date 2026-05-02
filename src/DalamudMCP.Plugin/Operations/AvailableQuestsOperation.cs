using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using MemoryPack;
using GameUiMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "quest.available",
    Description = "Gets currently visible, unaccepted quests in the current zone.",
    Summary = "Gets available quests in the current zone.")]
[ResultFormatter(typeof(AvailableQuestsOperation.TextFormatter))]
[CliCommand("quest", "available")]
[McpTool("get_available_quests")]
public sealed partial class AvailableQuestsOperation
    : IOperation<AvailableQuestsOperation.Request, AvailableQuestsSnapshot>, IPluginReaderStatus
{
    private static readonly ClientLanguage[] SearchLanguages =
    [
        ClientLanguage.English,
        ClientLanguage.Japanese,
        ClientLanguage.German,
        ClientLanguage.French
    ];

    private readonly Func<Request, CancellationToken, ValueTask<AvailableQuestsSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public AvailableQuestsOperation(
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

    internal AvailableQuestsOperation(
        Func<Request, CancellationToken, ValueTask<AvailableQuestsSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "quest.available";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<AvailableQuestsSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("quest.available")]
    public sealed partial class Request
    {
        [Option("name-contains", Description = "Optional substring used to filter visible quest names.", Required = false)]
        public string? NameContains { get; init; }

        [Option("max-results", Description = "Maximum number of quests to return.", Required = false)]
        public int? MaxResults { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<AvailableQuestsSnapshot>
    {
        public string? FormatText(AvailableQuestsSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AvailableQuestsSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? nameContains = NormalizeNameFilter(request.NameContains);
            int maxResults = NormalizeMaxResults(request.MaxResults);

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, dataManager, nameContains, maxResults, cancellationToken);

            return await framework.RunOnFrameworkThread(
                    () => ReadCurrentCore(clientState, dataManager, nameContains, maxResults, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AvailableQuestsSnapshot ReadCurrentCore(
        IClientState clientState,
        IDataManager dataManager,
        string? nameContains,
        int maxResults,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Available quests are not available because the local player is not logged in.");

        GameUiMap* map = GameUiMap.Instance();
        QuestManager* questManager = QuestManager.Instance();
        UIState* uiState = UIState.Instance();
        if (map is null || questManager is null || uiState is null)
            throw new InvalidOperationException("Quest marker systems are not available.");

        List<AvailableQuest> quests = new(Math.Min(maxResults, 16));
        HashSet<uint> seenQuestIds = [];
        foreach (MarkerInfo marker in map->UnacceptedQuestMarkers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            uint questId = marker.ObjectiveId;
            if (questId == 0 || !marker.ShouldRender || !seenQuestIds.Add(questId))
                continue;

            if (questManager->IsQuestAccepted(questId) ||
                uiState->IsUnlockLinkUnlockedOrQuestCompleted(questId, 0, true))
            {
                continue;
            }

            Quest? row = ResolveQuestRow(clientState, dataManager, questId);
            string? markerLabel = ReadMarkerLabel(marker);
            string questName = row is Quest questRow
                ? ReadQuestName(questRow)
                : markerLabel ?? $"Quest#{questId.ToString(CultureInfo.InvariantCulture)}";

            if (!MatchesName(questName, markerLabel, nameContains))
                continue;

            AvailableQuestMarker? markerSnapshot = CreatePrimaryMarker(marker);
            int? questLevel = row is Quest levelRow
                ? TryReadQuestLevel(levelRow) ?? NormalizeRecommendedLevel(marker.RecommendedLevel)
                : NormalizeRecommendedLevel(marker.RecommendedLevel);
            string territoryName = $"Territory#{clientState.TerritoryType.ToString(CultureInfo.InvariantCulture)}";

            quests.Add(new AvailableQuest(
                questId,
                questName,
                questLevel,
                markerSnapshot,
                BuildSummary(questId, questName, questLevel, territoryName)));

            if (quests.Count >= maxResults)
                break;
        }

        ushort territoryType = checked((ushort)clientState.TerritoryType);
        string summaryText = BuildSnapshotSummary(territoryType, nameContains, quests.Count);
        return new AvailableQuestsSnapshot(
            DateTimeOffset.UtcNow,
            territoryType,
            nameContains,
            quests.ToArray(),
            summaryText);
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

    private static bool MatchesName(string questName, string? markerLabel, string? nameContains)
    {
        if (string.IsNullOrWhiteSpace(nameContains))
            return true;

        return questName.Contains(nameContains, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(markerLabel) &&
                markerLabel.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
    }

    [SupportedOSPlatform("windows")]
    private static AvailableQuestMarker? CreatePrimaryMarker(MarkerInfo marker)
    {
        if (marker.MarkerData.Count <= 0)
            return null;

        MapMarkerData markerData = marker.MarkerData[0];
        return new AvailableQuestMarker(
            marker.MarkerData.Count,
            markerData.DataId == 0 ? null : markerData.DataId,
            markerData.MapId == 0 ? null : markerData.MapId,
            markerData.TerritoryTypeId == 0 ? null : markerData.TerritoryTypeId,
            Math.Round(markerData.Radius, 1),
            new AvailableQuestPosition(
                Math.Round(markerData.Position.X, 1),
                Math.Round(markerData.Position.Y, 1),
                Math.Round(markerData.Position.Z, 1)));
    }

    private static string? ReadMarkerLabel(MarkerInfo marker)
    {
        string? label = marker.Label.ToString();
        return string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    private static string ReadQuestName(Quest row)
    {
        return TryReadStringProperty(row, "Name")
               ?? TryReadStringProperty(row, "NameEnglish")
               ?? $"Quest#{row.RowId.ToString(CultureInfo.InvariantCulture)}";
    }

    private static int? TryReadQuestLevel(Quest row)
    {
        return TryReadIntProperty(row, "ClassJobLevel0")
               ?? TryReadIntProperty(row, "ClassJobLevel");
    }

    private static string? TryReadStringProperty(object row, string propertyName)
    {
        PropertyInfo? property = row.GetType().GetProperty(propertyName);
        object? value = property?.GetValue(row);
        string? text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? TryReadIntProperty(object row, string propertyName)
    {
        PropertyInfo? property = row.GetType().GetProperty(propertyName);
        object? value = property?.GetValue(row);
        if (value is null)
            return null;

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static int? NormalizeRecommendedLevel(ushort recommendedLevel)
    {
        return recommendedLevel == 0 ? null : recommendedLevel;
    }

    private static string BuildSummary(uint questId, string questName, int? questLevel, string territoryName)
    {
        string levelText = questLevel is null
            ? "level unknown"
            : $"level {questLevel.Value.ToString(CultureInfo.InvariantCulture)}";
        return $"{questName} ({questId.ToString(CultureInfo.InvariantCulture)}) is available in {territoryName} ({levelText}).";
    }

    private static string BuildSnapshotSummary(ushort territoryType, string? nameContains, int count)
    {
        string territoryName = $"Territory#{territoryType.ToString(CultureInfo.InvariantCulture)}";
        if (count == 0)
        {
            return string.IsNullOrWhiteSpace(nameContains)
                ? $"No visible unaccepted quests were found in {territoryName}."
                : $"No visible unaccepted quests matching '{nameContains}' were found in {territoryName}.";
        }

        return string.IsNullOrWhiteSpace(nameContains)
            ? $"{count.ToString(CultureInfo.InvariantCulture)} visible unaccepted quests found in {territoryName}."
            : $"{count.ToString(CultureInfo.InvariantCulture)} visible unaccepted quests matching '{nameContains}' found in {territoryName}.";
    }

    private static string? NormalizeNameFilter(string? nameContains)
    {
        return string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
    }

    private static int NormalizeMaxResults(int? maxResults)
    {
        return maxResults is null ? 16 : Math.Clamp(maxResults.Value, 1, 64);
    }
}

[MemoryPackable]
public sealed partial record AvailableQuestPosition(
    double X,
    double Y,
    double Z);

[MemoryPackable]
public sealed partial record AvailableQuestMarker(
    int MarkerCount,
    uint? IssuerDataId,
    uint? MapId,
    ushort? TerritoryTypeId,
    double Radius,
    AvailableQuestPosition? Position);

[MemoryPackable]
public sealed partial record AvailableQuest(
    uint QuestId,
    string QuestName,
    int? QuestLevel,
    AvailableQuestMarker? Marker,
    string SummaryText);

[MemoryPackable]
public sealed partial record AvailableQuestsSnapshot(
    DateTimeOffset CapturedAt,
    ushort TerritoryTypeId,
    string? NameContains,
    AvailableQuest[] Quests,
    string SummaryText);