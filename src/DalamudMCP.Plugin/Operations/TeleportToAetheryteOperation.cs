using System.Runtime.Versioning;
using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "teleport.to.aetheryte",
    Description = "Teleports to an unlocked aetheryte by query.",
    Summary = "Teleports to an aetheryte.")]
[ResultFormatter(typeof(TeleportToAetheryteOperation.TextFormatter))]
[CliCommand("teleport", "to", "aetheryte")]
[McpTool("teleport_to_aetheryte")]
public sealed partial class TeleportToAetheryteOperation : IOperation<TeleportToAetheryteOperation.Request, TeleportToAetheryteResult>
{
    private static readonly ClientLanguage[] SearchLanguages =
    [
        ClientLanguage.English,
        ClientLanguage.Japanese,
        ClientLanguage.German,
        ClientLanguage.French
    ];

    private readonly Func<Request, CancellationToken, ValueTask<TeleportToAetheryteResult>> executor;

    [SupportedOSPlatform("windows")]
    public TeleportToAetheryteOperation(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(dataManager);

        executor = CreateDalamudExecutor(new LifestreamAethernetClient(pluginInterface), framework, clientState, dataManager);
    }

    internal TeleportToAetheryteOperation(Func<Request, CancellationToken, ValueTask<TeleportToAetheryteResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<TeleportToAetheryteResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("teleport.to.aetheryte")]
    [LegacyBridgeRequest("TeleportToAetheryte")]
    public sealed partial class Request
    {
        [Option("query", Description = "Aetheryte name or alias to search for.")]
        public string Query { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<TeleportToAetheryteResult>
    {
        public string? FormatText(TeleportToAetheryteResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<TeleportToAetheryteResult>> CreateDalamudExecutor(
        ILifestreamAethernetClient lifestream,
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string query = NormalizeQuery(request.Query);

            if (framework.IsInFrameworkUpdateThread)
                return TeleportCore(clientState, dataManager, lifestream, query, cancellationToken);

            return await framework.RunOnFrameworkThread(() => TeleportCore(clientState, dataManager, lifestream, query, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe TeleportToAetheryteResult TeleportCore(
        IClientState clientState,
        IDataManager dataManager,
        ILifestreamAethernetClient lifestream,
        string query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            return CreateFailure(query, "not_logged_in", "Teleport is unavailable before the character is fully logged in.");

        Telepo* telepo = Telepo.Instance();
        if (telepo is null)
            return CreateFailure(query, "telepo_unavailable", "Telepo is not available.");

        telepo->UpdateAetheryteList();
        Span<TeleportInfo> teleportInfos = telepo->TeleportList.AsSpan();
        if (teleportInfos.Length == 0)
        {
            TeleportToAetheryteResult? lifestreamResult = TryStartLifestreamAethernetTeleport(lifestream, query);
            if (lifestreamResult is not null)
                return lifestreamResult;

            return CreateFailure(
                query,
                "teleport_list_empty",
                "No unlocked teleport destinations are currently available.");
        }

        ResolvedDestination? destination = FindBestMatch(query, teleportInfos, dataManager, clientState.ClientLanguage);
        if (destination is null)
        {
            TeleportToAetheryteResult? lifestreamResult = TryStartLifestreamAethernetTeleport(lifestream, query);
            if (lifestreamResult is not null)
                return lifestreamResult;

            return CreateFailure(query, "destination_not_found", $"No unlocked aetheryte matched '{query}'.");
        }

        bool success = telepo->Teleport(destination.Value.Info.AetheryteId, destination.Value.Info.SubIndex);
        return new TeleportToAetheryteResult(
            query,
            success,
            success ? null : "teleport_rejected",
            destination.Value.Info.AetheryteId,
            destination.Value.AetheryteName,
            destination.Value.TerritoryName,
            success
                ? $"Teleport started to {destination.Value.AetheryteName}."
                : $"Teleport could not be started for {destination.Value.AetheryteName}.");
    }

    private static ResolvedDestination? FindBestMatch(
        string query,
        ReadOnlySpan<TeleportInfo> teleportInfos,
        IDataManager dataManager,
        ClientLanguage clientLanguage)
    {
        string normalizedQuery = NormalizeForSearch(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return null;

        HashSet<string> aliases = GetAliases(normalizedQuery);
        List<ResolvedDestination> destinations = ResolveDestinations(teleportInfos, dataManager, clientLanguage);

        foreach (ResolvedDestination destination in destinations)
            if (destination.SearchTerms.Any(term => aliases.Contains(term, StringComparer.Ordinal)))
                return destination;

        foreach (ResolvedDestination destination in destinations)
            if (destination.SearchTerms.Any(term => term.Contains(normalizedQuery, StringComparison.Ordinal)))
                return destination;

        return null;
    }

    private static List<ResolvedDestination> ResolveDestinations(
        ReadOnlySpan<TeleportInfo> teleportInfos,
        IDataManager dataManager,
        ClientLanguage clientLanguage)
    {
        List<ResolvedDestination> destinations = new(teleportInfos.Length);
        for (int index = 0; index < teleportInfos.Length; index++)
        {
            ResolvedDestination? resolved = ResolveDestination(teleportInfos[index], dataManager, clientLanguage);
            if (resolved is not null)
                destinations.Add(resolved.Value);
        }

        return destinations;
    }

    private static ResolvedDestination? ResolveDestination(
        TeleportInfo info,
        IDataManager dataManager,
        ClientLanguage clientLanguage)
    {
        foreach (ClientLanguage? language in EnumerateLanguages(clientLanguage))
        {
            ExcelSheet<Aetheryte>? sheet = dataManager.GetExcelSheet<Aetheryte>(language);
            if (sheet is null)
                continue;

            foreach (Aetheryte row in sheet)
            {
                if (row.RowId != info.AetheryteId)
                    continue;

                string aetheryteName = ReadAetheryteName(row);
                string territoryName = ReadTerritoryName(row);
                return new ResolvedDestination(
                    info,
                    aetheryteName,
                    territoryName,
                    BuildSearchTerms(aetheryteName, territoryName));
            }
        }

        return null;
    }

    private static IEnumerable<ClientLanguage?> EnumerateLanguages(ClientLanguage clientLanguage)
    {
        yield return clientLanguage;
        foreach (ClientLanguage language in SearchLanguages)
            if (language != clientLanguage)
                yield return language;
    }

    private static string NormalizeQuery(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? throw new ArgumentException("query is required.", nameof(query))
            : query.Trim();
    }

    private static string ReadAetheryteName(Aetheryte row)
    {
        string? placeName = row.PlaceName.ValueNullable?.Name.ToString();
        if (!string.IsNullOrWhiteSpace(placeName))
            return placeName;

        string? aethernetName = row.AethernetName.ValueNullable?.Name.ToString();
        return string.IsNullOrWhiteSpace(aethernetName) ? $"Aetheryte#{row.RowId}" : aethernetName;
    }

    private static string ReadTerritoryName(Aetheryte row)
    {
        string? territoryName = row.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToString();
        return string.IsNullOrWhiteSpace(territoryName) ? $"Territory#{row.Territory.RowId}" : territoryName;
    }

    internal static IReadOnlyCollection<string> BuildSearchTerms(string aetheryteName, string territoryName)
    {
        HashSet<string> terms = new(StringComparer.Ordinal);
        foreach (string value in new[]
                 {
                     NormalizeForSearch(aetheryteName),
                     NormalizeForSearch(territoryName),
                     NormalizeForSearch(string.Concat(aetheryteName, territoryName)),
                     NormalizeForSearch(string.Concat(territoryName, aetheryteName))
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            terms.Add(value);
            foreach (string alias in GetAliases(value))
                terms.Add(alias);
        }

        return terms.ToArray();
    }

    internal static HashSet<string> GetAliases(string normalizedValue)
    {
        HashSet<string> aliases = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return aliases;

        aliases.Add(normalizedValue);
        if (normalizedValue.Contains("goldsaucer", StringComparison.Ordinal) ||
            normalizedValue.Contains("ゴールドソーサー", StringComparison.Ordinal))
        {
            aliases.Add("goldsaucer");
            aliases.Add("thegoldsaucer");
            aliases.Add("mandervillegoldsaucer");
            aliases.Add("ゴールドソーサー");
            aliases.Add("マンダヴィルゴールドソーサー");
        }

        return aliases;
    }

    internal static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Concat(value.Where(static character => char.IsLetterOrDigit(character))).ToLowerInvariant();
    }

    internal static TeleportToAetheryteResult? TryStartLifestreamAethernetTeleport(
        ILifestreamAethernetClient? client,
        string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (client is null || !client.IsAvailable)
            return null;

        bool succeeded;
        string? reason = null;
        try
        {
            if (client.IsBusy)
            {
                succeeded = false;
                reason = "lifestream_busy";
            }
            else
            {
                succeeded = client.StartAethernetTeleport(query);
                if (!succeeded)
                    reason = "lifestream_rejected";
            }
        }
        catch
        {
            succeeded = false;
            reason = "lifestream_ipc_error";
        }

        return new TeleportToAetheryteResult(
            query,
            succeeded,
            succeeded ? null : reason,
            null,
            query,
            null,
            succeeded
                ? $"Lifestream started local aethernet travel to {query}."
                : BuildLifestreamFailureSummary(query, reason));
    }

    private static string BuildLifestreamFailureSummary(string query, string? reason)
    {
        return reason switch
        {
            "lifestream_busy" => $"Lifestream is busy and could not start local aethernet travel to {query}.",
            "lifestream_ipc_error" => $"Lifestream reported an IPC error while starting local aethernet travel to {query}.",
            _ => $"Lifestream could not start local aethernet travel to {query}."
        };
    }

    private static TeleportToAetheryteResult CreateFailure(string query, string reason, string summary)
    {
        return new TeleportToAetheryteResult(query, false, reason, null, null, null, summary);
    }

    private readonly record struct ResolvedDestination(
        TeleportInfo Info,
        string AetheryteName,
        string TerritoryName,
        IReadOnlyCollection<string> SearchTerms);

    internal interface ILifestreamAethernetClient
    {
        public bool IsAvailable { get; }

        public bool IsBusy { get; }

        public bool StartAethernetTeleport(string destination);
    }

    private sealed class LifestreamAethernetClient : ILifestreamAethernetClient
    {
        private readonly ICallGateSubscriber<string, bool> aethernetTeleportSubscriber;
        private readonly ICallGateSubscriber<bool> isBusySubscriber;

        public LifestreamAethernetClient(IDalamudPluginInterface pluginInterface)
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);
            aethernetTeleportSubscriber = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.AethernetTeleport");
            isBusySubscriber = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        }

        public bool IsAvailable => aethernetTeleportSubscriber.HasFunction;

        public bool IsBusy => isBusySubscriber.HasFunction && isBusySubscriber.InvokeFunc();

        public bool StartAethernetTeleport(string destination)
        {
            return aethernetTeleportSubscriber.HasFunction && aethernetTeleportSubscriber.InvokeFunc(destination);
        }
    }
}

[MemoryPackable]
public sealed partial record TeleportToAetheryteResult(
    string RequestedQuery,
    bool Succeeded,
    string? Reason,
    uint? AetheryteId,
    string? AetheryteName,
    string? TerritoryName,
    string SummaryText);
