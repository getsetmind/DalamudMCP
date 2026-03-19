using System.Runtime.Versioning;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed unsafe class DalamudAetheryteTeleportController : IAetheryteTeleportController
{
    private static readonly ClientLanguage[] SearchLanguages =
    [
        ClientLanguage.English,
        ClientLanguage.Japanese,
        ClientLanguage.German,
        ClientLanguage.French,
    ];

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;

    public DalamudAetheryteTeleportController(
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(dataManager);
        this.framework = framework;
        this.clientState = clientState;
        this.dataManager = dataManager;
    }

    public Task<TeleportToAetheryteResult> TeleportAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => TeleportCore(query.Trim(), cancellationToken));
        }

        return Task.FromResult(TeleportCore(query.Trim(), cancellationToken));
    }

    private TeleportToAetheryteResult TeleportCore(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return CreateFailure(query, "not_logged_in", "Teleport is unavailable before the character is fully logged in.");
        }

        var telepo = Telepo.Instance();
        if (telepo is null)
        {
            return CreateFailure(query, "telepo_unavailable", "Telepo is not available.");
        }

        telepo->UpdateAetheryteList();
        var teleportInfos = telepo->TeleportList.AsSpan();
        if (teleportInfos.Length == 0)
        {
            return CreateFailure(query, "teleport_list_empty", "No unlocked teleport destinations are currently available.");
        }

        var destination = FindBestMatch(query, teleportInfos);
        if (destination is null)
        {
            return CreateFailure(query, "destination_not_found", $"No unlocked aetheryte matched '{query}'.");
        }

        var success = telepo->Teleport(destination.Value.Info.AetheryteId, destination.Value.Info.SubIndex);
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

    private ResolvedDestination? FindBestMatch(string query, ReadOnlySpan<TeleportInfo> teleportInfos)
    {
        var normalizedQuery = Normalize(query);
        var aliases = GetAliases(normalizedQuery);
        var destinations = ResolveDestinations(teleportInfos);

        foreach (var destination in destinations)
        {
            if (destination.SearchTerms.Any(term => aliases.Contains(term, StringComparer.Ordinal)))
            {
                return destination;
            }
        }

        foreach (var destination in destinations)
        {
            if (destination.SearchTerms.Any(term => term.Contains(normalizedQuery, StringComparison.Ordinal)))
            {
                return destination;
            }
        }

        return null;
    }

    private List<ResolvedDestination> ResolveDestinations(ReadOnlySpan<TeleportInfo> teleportInfos)
    {
        var destinations = new List<ResolvedDestination>(teleportInfos.Length);
        for (var index = 0; index < teleportInfos.Length; index++)
        {
            var info = teleportInfos[index];
            var resolved = ResolveDestination(info);
            if (resolved is not null)
            {
                destinations.Add(resolved.Value);
            }
        }

        return destinations;
    }

    private ResolvedDestination? ResolveDestination(TeleportInfo info)
    {
        foreach (var language in EnumerateLanguages())
        {
            var sheet = dataManager.GetExcelSheet<Aetheryte>(language);
            if (sheet is null)
            {
                continue;
            }

            foreach (var row in sheet)
            {
                if (row.RowId != info.AetheryteId)
                {
                    continue;
                }

                var aetheryteName = ReadAetheryteName(row);
                var territoryName = ReadTerritoryName(row);
                var terms = new HashSet<string>(StringComparer.Ordinal)
                {
                    Normalize(aetheryteName),
                    Normalize(territoryName),
                };

                foreach (var alias in GetAliases(Normalize(aetheryteName)))
                {
                    terms.Add(alias);
                }

                foreach (var alias in GetAliases(Normalize(territoryName)))
                {
                    terms.Add(alias);
                }

                return new ResolvedDestination(
                    info,
                    aetheryteName,
                    territoryName,
                    terms.Where(static term => !string.IsNullOrWhiteSpace(term)).ToArray());
            }
        }

        return null;
    }

    private IEnumerable<ClientLanguage?> EnumerateLanguages()
    {
        yield return clientState.ClientLanguage;
        foreach (var language in SearchLanguages)
        {
            if (language != clientState.ClientLanguage)
            {
                yield return language;
            }
        }
    }

    private static string ReadAetheryteName(Aetheryte row)
    {
        var placeName = row.PlaceName.ValueNullable?.Name.ToString();
        if (!string.IsNullOrWhiteSpace(placeName))
        {
            return placeName;
        }

        var aethernetName = row.AethernetName.ValueNullable?.Name.ToString();
        if (!string.IsNullOrWhiteSpace(aethernetName))
        {
            return aethernetName;
        }

        return $"Aetheryte#{row.RowId}";
    }

    private static string ReadTerritoryName(Aetheryte row)
    {
        var territoryName = row.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToString();
        return string.IsNullOrWhiteSpace(territoryName) ? $"Territory#{row.Territory.RowId}" : territoryName;
    }

    private static HashSet<string> GetAliases(string normalizedValue)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return aliases;
        }

        aliases.Add(normalizedValue);
        if (normalizedValue.Contains("goldsaucer", StringComparison.Ordinal) || normalizedValue.Contains("ゴールドソーサー", StringComparison.Ordinal))
        {
            aliases.Add("goldsaucer");
            aliases.Add("thegoldsaucer");
            aliases.Add("mandervillegoldsaucer");
            aliases.Add("ゴールドソーサー");
            aliases.Add("マンダヴィルゴールドソーサー");
        }

        return aliases;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Where(static character => !char.IsWhiteSpace(character))).Trim().ToLowerInvariant();
    }

    private static TeleportToAetheryteResult CreateFailure(string query, string reason, string summary) =>
        new(
            query,
            Succeeded: false,
            Reason: reason,
            AetheryteId: null,
            AetheryteName: null,
            TerritoryName: null,
            SummaryText: summary);

    private readonly record struct ResolvedDestination(
        TeleportInfo Info,
        string AetheryteName,
        string TerritoryName,
        IReadOnlyCollection<string> SearchTerms);
}
