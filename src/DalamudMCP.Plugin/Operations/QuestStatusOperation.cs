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

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "quest.status",
    Description = "Gets quest status matches.",
    Summary = "Gets quest status matches.")]
[ResultFormatter(typeof(QuestStatusOperation.TextFormatter))]
[CliCommand("quest", "status")]
[McpTool("get_quest_status")]
public sealed partial class QuestStatusOperation
    : IOperation<QuestStatusOperation.Request, QuestStatusSnapshot>, IPluginReaderStatus
{
    private static readonly ClientLanguage[] SearchLanguages =
    [
        ClientLanguage.English,
        ClientLanguage.Japanese,
        ClientLanguage.German,
        ClientLanguage.French
    ];

    private readonly Func<Request, CancellationToken, ValueTask<QuestStatusSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public QuestStatusOperation(
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

    internal QuestStatusOperation(
        Func<Request, CancellationToken, ValueTask<QuestStatusSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "quest.status";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<QuestStatusSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("quest.status")]
    [LegacyBridgeRequest("GetQuestStatus")]
    public sealed partial class Request
    {
        [Option("quest-id", Description = "Optional quest id to match.", Required = false)]
        public uint? QuestId { get; init; }

        [Option("query", Description = "Optional text query to match.", Required = false)]
        public string? Query { get; init; }

        [Option("max-results", Description = "Maximum number of results to return.", Required = false)]
        public int? MaxResults { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<QuestStatusSnapshot>
    {
        public string? FormatText(QuestStatusSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<QuestStatusSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint? questId = request.QuestId;
            string? query = NormalizeQuery(request.Query);
            int maxResults = NormalizeMaxResults(request.MaxResults);
            if (questId is null && string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Either quest-id or query must be provided.", nameof(request));

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, dataManager, questId, query, maxResults, cancellationToken);

            return await framework.RunOnFrameworkThread(
                    () => ReadCurrentCore(clientState, dataManager, questId, query, maxResults, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe QuestStatusSnapshot ReadCurrentCore(
        IClientState clientState,
        IDataManager dataManager,
        uint? questId,
        string? query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Quest status is not available because the local player is not logged in.");

        QuestManager* questManager = QuestManager.Instance();
        UIState* uiState = UIState.Instance();
        if (questManager is null || uiState is null)
            throw new InvalidOperationException("Quest systems are not available.");

        QuestStatusEntrySnapshot[] matches = ResolveMatches(clientState, dataManager, questId, query, maxResults, questManager, uiState);
        string normalizedQuery = questId is not null
            ? $"questId:{questId.Value.ToString(CultureInfo.InvariantCulture)}"
            : query ?? string.Empty;

        return new QuestStatusSnapshot(
            DateTimeOffset.UtcNow,
            normalizedQuery,
            matches,
            matches.Length == 0
                ? $"No quests matched '{normalizedQuery}'."
                : $"{matches.Length.ToString(CultureInfo.InvariantCulture)} quest entries matched '{normalizedQuery}'.");
    }

    [SupportedOSPlatform("windows")]
    private static unsafe QuestStatusEntrySnapshot[] ResolveMatches(
        IClientState clientState,
        IDataManager dataManager,
        uint? questId,
        string? query,
        int maxResults,
        QuestManager* questManager,
        UIState* uiState)
    {
        if (questId is not null)
        {
            Quest? row = ResolveQuestRow(clientState, dataManager, questId.Value);
            return row is null ? [] : [CreateEntrySnapshot(row.Value, questManager, uiState)];
        }

        string normalizedQuery = NormalizeForSearch(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return [];

        foreach (ClientLanguage? language in EnumerateLanguages(clientState))
        {
            ExcelSheet<Quest>? sheet = dataManager.GetExcelSheet<Quest>(language);
            if (sheet is null)
                continue;

            QuestStatusEntrySnapshot[] matches = sheet
                .Where(row => MatchesQuery(row, normalizedQuery))
                .Take(maxResults)
                .Select(row => CreateEntrySnapshot(row, questManager, uiState))
                .ToArray();
            if (matches.Length > 0)
                return matches;
        }

        return [];
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

    [SupportedOSPlatform("windows")]
    private static unsafe QuestStatusEntrySnapshot CreateEntrySnapshot(Quest row, QuestManager* questManager, UIState* uiState)
    {
        bool accepted = questManager->IsQuestAccepted(row.RowId);
        byte? sequence = accepted ? QuestManager.GetQuestSequence(row.RowId) : null;
        bool completed = uiState->IsUnlockLinkUnlockedOrQuestCompleted(row.RowId, 0, true);
        string questName = ReadQuestName(row);

        return new QuestStatusEntrySnapshot(
            row.RowId,
            questName,
            accepted,
            completed,
            sequence,
            TryReadQuestLevel(row),
            BuildSummary(row.RowId, questName, accepted, completed, sequence));
    }

    private static bool MatchesQuery(Quest row, string normalizedQuery)
    {
        return NormalizeForSearch(ReadQuestName(row)).Contains(normalizedQuery, StringComparison.Ordinal);
    }

    private static string ReadQuestName(Quest row)
    {
        return TryReadStringProperty(row, "Name")
               ?? TryReadStringProperty(row, "NameEnglish")
               ?? $"Quest#{row.RowId}";
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

    private static string BuildSummary(uint questId, string questName, bool accepted, bool completed, byte? sequence)
    {
        if (completed)
            return $"{questName} ({questId.ToString(CultureInfo.InvariantCulture)}) is completed.";

        if (accepted)
        {
            return
                $"{questName} ({questId.ToString(CultureInfo.InvariantCulture)}) is accepted at sequence {sequence?.ToString(CultureInfo.InvariantCulture) ?? "?"}.";
        }

        return $"{questName} ({questId.ToString(CultureInfo.InvariantCulture)}) is not accepted.";
    }

    private static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Concat(value.Where(static character => char.IsLetterOrDigit(character))).ToLowerInvariant();
    }

    private static string? NormalizeQuery(string? query)
    {
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    private static int NormalizeMaxResults(int? maxResults)
    {
        return maxResults is null ? 8 : Math.Clamp(maxResults.Value, 1, 20);
    }
}

[MemoryPackable]
public sealed partial record QuestStatusEntrySnapshot(
    uint QuestId,
    string QuestName,
    bool IsAccepted,
    bool IsCompleted,
    byte? Sequence,
    int? QuestLevel,
    string SummaryText);

[MemoryPackable]
public sealed partial record QuestStatusSnapshot(
    DateTimeOffset CapturedAt,
    string Query,
    QuestStatusEntrySnapshot[] Matches,
    string SummaryText);



