using System.Runtime.Versioning;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using InteropGenerator.Runtime;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.select.menu-item",
    Description = "Selects a menu item by label for supported addons such as SelectString and TelepotTown.",
    Summary = "Selects an addon menu item by label.")]
[ResultFormatter(typeof(AddonSelectMenuItemOperation.TextFormatter))]
[CliCommand("addon", "select", "menu-item")]
[McpTool("select_addon_menu_item")]
public sealed partial class AddonSelectMenuItemOperation : IOperation<AddonSelectMenuItemOperation.Request, AddonSelectMenuItemResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<AddonSelectMenuItemResult>> executor;

    [SupportedOSPlatform("windows")]
    public AddonSelectMenuItemOperation(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);

        executor = CreateDalamudExecutor(framework, clientState, gameGui);
    }

    internal AddonSelectMenuItemOperation(Func<Request, CancellationToken, ValueTask<AddonSelectMenuItemResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<AddonSelectMenuItemResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.select.menu-item")]
    public sealed partial class Request
    {
        [Option("addon", Description = "Addon name to target.")]
        public string AddonName { get; init; } = string.Empty;

        [Option("label", Description = "Visible menu item label to select.")]
        public string Label { get; init; } = string.Empty;

        [Option("contains-match", Description = "When true, allows substring matching instead of exact matching.", Required = false)]
        public bool ContainsMatch { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<AddonSelectMenuItemResult>
    {
        public string? FormatText(AddonSelectMenuItemResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AddonSelectMenuItemResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string addonName = NormalizeRequiredText(request.AddonName, nameof(request.AddonName));
            string requestedLabel = NormalizeRequiredText(request.Label, nameof(request.Label));

            if (framework.IsInFrameworkUpdateThread)
            {
                return SelectMenuItemCore(
                    gameGui,
                    addonName,
                    requestedLabel,
                    request.ContainsMatch,
                    cancellationToken);
            }

            return await framework.RunOnFrameworkThread(
                    () => SelectMenuItemCore(
                        gameGui,
                        addonName,
                        requestedLabel,
                        request.ContainsMatch,
                        cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonSelectMenuItemResult SelectMenuItemCore(
        IGameGui gameGui,
        string addonName,
        string requestedLabel,
        bool containsMatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetReadyAddon(gameGui, addonName, out string reason, out string summary))
            return CreateFailure(addonName, requestedLabel, reason, summary);

        if (string.Equals(addonName, "SelectString", StringComparison.Ordinal))
        {
            AddonSelectString* addon = gameGui.GetAddonByName<AddonSelectString>(addonName, 1);
            return SelectSelectStringItem(addonName, requestedLabel, containsMatch, addon);
        }

        if (string.Equals(addonName, "SelectIconString", StringComparison.Ordinal))
        {
            AddonSelectIconString* addon = gameGui.GetAddonByName<AddonSelectIconString>(addonName, 1);
            return SelectSelectIconStringItem(addonName, requestedLabel, containsMatch, addon);
        }

        if (string.Equals(addonName, "TelepotTown", StringComparison.Ordinal))
        {
            AddonTeleportTown* addon = gameGui.GetAddonByName<AddonTeleportTown>(addonName, 1);
            AtkUnitBasePtr addonWrapper = gameGui.GetAddonByName(addonName, 1);
            return SelectTelepotTownItem(addonName, requestedLabel, containsMatch, addon, addonWrapper);
        }

        return CreateFailure(
            addonName,
            requestedLabel,
            "unsupported_addon",
            $"{addonName} is not a supported menu addon. Supported addons: SelectString, SelectIconString, TelepotTown.");
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonSelectMenuItemResult SelectSelectStringItem(
        string addonName,
        string requestedLabel,
        bool containsMatch,
        AddonSelectString* addon)
    {
        if (addon == null)
            return CreateFailure(addonName, requestedLabel, "addon_struct_unavailable", $"{addonName} did not expose a native addon pointer.");

        if (addon->PopupMenu.EntryNames == null || addon->PopupMenu.EntryCount <= 0)
        {
            return CreateFailure(
                addonName,
                requestedLabel,
                "popup_entries_unavailable",
                $"{addonName} did not expose selectable popup menu entries.");
        }

        return SelectPopupMenuItem(
            addonName,
            requestedLabel,
            containsMatch,
            addon->PopupMenu.EntryNames,
            addon->PopupMenu.EntryCount,
            &addon->AtkUnitBase);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonSelectMenuItemResult SelectSelectIconStringItem(
        string addonName,
        string requestedLabel,
        bool containsMatch,
        AddonSelectIconString* addon)
    {
        if (addon == null)
            return CreateFailure(addonName, requestedLabel, "addon_struct_unavailable", $"{addonName} did not expose a native addon pointer.");

        if (addon->PopupMenu.EntryNames == null || addon->PopupMenu.EntryCount <= 0)
        {
            return CreateFailure(
                addonName,
                requestedLabel,
                "popup_entries_unavailable",
                $"{addonName} did not expose selectable popup menu entries.");
        }

        return SelectPopupMenuItem(
            addonName,
            requestedLabel,
            containsMatch,
            addon->PopupMenu.EntryNames,
            addon->PopupMenu.EntryCount,
            &addon->AtkUnitBase);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonSelectMenuItemResult SelectPopupMenuItem(
        string addonName,
        string requestedLabel,
        bool containsMatch,
        CStringPointer* entryNames,
        int entryCount,
        AtkUnitBase* unitBase)
    {
        MenuItemCandidate[] candidates = ReadPopupMenuCandidates(entryNames, entryCount);
        MenuItemMatch? match = TryFindMenuItemMatch(candidates, requestedLabel, containsMatch);
        if (match is null)
        {
            return CreateFailure(
                addonName,
                requestedLabel,
                "label_not_found",
                BuildLabelNotFoundSummary(addonName, requestedLabel, containsMatch, candidates));
        }

        if (unitBase == null)
            return CreateFailure(addonName, requestedLabel, "addon_struct_unavailable", $"{addonName} did not expose an AtkUnitBase.");

        bool succeeded = unitBase->FireCallbackInt(match.SelectionIndex);
        return succeeded
            ? new AddonSelectMenuItemResult(
                addonName,
                requestedLabel,
                match.Label,
                match.SelectionIndex,
                "popup-menu-callback",
                true,
                null,
                $"Selected '{match.Label}' from {addonName} using callback index {match.SelectionIndex}.")
            : CreateFailure(
                addonName,
                requestedLabel,
                "selection_failed",
                $"Failed to select '{match.Label}' from {addonName} using callback index {match.SelectionIndex}.",
                matchedLabel: match.Label,
                matchedIndex: match.SelectionIndex,
                method: "popup-menu-callback");
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonSelectMenuItemResult SelectTelepotTownItem(
        string addonName,
        string requestedLabel,
        bool containsMatch,
        AddonTeleportTown* addon,
        AtkUnitBasePtr addonWrapper)
    {
        if (addon == null)
            return CreateFailure(addonName, requestedLabel, "addon_struct_unavailable", $"{addonName} did not expose a native addon pointer.");

        AgentTelepotTown* agent = AgentTelepotTown.Instance();
        if (agent == null || !agent->IsAgentActive())
        {
            return CreateFailure(
                addonName,
                requestedLabel,
                "teleport_agent_unavailable",
                "TelepotTown agent is unavailable.");
        }

        MenuItemCandidate[] candidates = ReadTelepotTownCandidates(addon, addonWrapper, agent->Data);
        MenuItemMatch? match = TryFindMenuItemMatch(candidates, requestedLabel, containsMatch);
        if (match is null)
        {
            return CreateFailure(
                addonName,
                requestedLabel,
                "label_not_found",
                BuildLabelNotFoundSummary(addonName, requestedLabel, containsMatch, candidates));
        }

        if (match.SelectionIndex < 0 || match.SelectionIndex > byte.MaxValue)
        {
            return CreateFailure(
                addonName,
                requestedLabel,
                "selection_index_out_of_range",
                $"Resolved selection index {match.SelectionIndex} for '{match.Label}' is invalid.",
                matchedLabel: match.Label,
                matchedIndex: match.SelectionIndex,
                method: "telepot-town-agent");
        }

        agent->TeleportToAetheryte((byte)match.SelectionIndex);
        return new AddonSelectMenuItemResult(
            addonName,
            requestedLabel,
            match.Label,
            match.SelectionIndex,
            "telepot-town-agent",
            true,
            null,
            $"Selected '{match.Label}' from {addonName} using teleport index {match.SelectionIndex}.");
    }

    [SupportedOSPlatform("windows")]
    private static unsafe MenuItemCandidate[] ReadPopupMenuCandidates(CStringPointer* entryNames, int entryCount)
    {
        List<MenuItemCandidate> candidates = [];
        for (int index = 0; index < entryCount; index++)
        {
            string? label = entryNames[index].ToString();
            if (string.IsNullOrWhiteSpace(label))
                continue;

            candidates.Add(new MenuItemCandidate(label, index, true));
        }

        return [.. candidates];
    }

    [SupportedOSPlatform("windows")]
    private static unsafe MenuItemCandidate[] ReadTelepotTownCandidates(
        AddonTeleportTown* addon,
        AtkUnitBasePtr addonWrapper,
        AgentTelepotTownData* data)
    {
        List<MenuItemCandidate> treeCandidates = [];
        if (addon->List != null)
        {
            int selectionIndex = 0;
            int maxSelectableCount = GetTelepotTownSelectableCount(data);
            foreach (Pointer<AtkComponentTreeListItem> itemPointer in addon->List->Items)
            {
                AtkComponentTreeListItem* item = itemPointer;
                if (item == null)
                    continue;

                string? label = ReadFirstNonEmptyString(item->StringValues);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                bool isSelectable = IsSelectableTelepotTownItem(item);
                if (!isSelectable)
                {
                    treeCandidates.Add(new MenuItemCandidate(label, -1, false));
                    continue;
                }

                if (selectionIndex >= maxSelectableCount)
                    break;

                treeCandidates.Add(new MenuItemCandidate(label, selectionIndex, true));
                selectionIndex++;
            }
        }

        MenuItemCandidate[] stringCandidates = ReadTelepotTownStringCandidates(addonWrapper, data);
        int selectableTreeCount = treeCandidates.Count(static candidate => candidate.IsSelectable);
        int selectableStringCount = stringCandidates.Count(static candidate => candidate.IsSelectable);
        return selectableStringCount > selectableTreeCount ? stringCandidates : [.. treeCandidates];
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool IsSelectableTelepotTownItem(AtkComponentTreeListItem* item)
    {
        AtkComponentTreeListItemType? itemType = TryReadTreeListItemType(item->UIntValues.AsSpan());
        return itemType switch
        {
            AtkComponentTreeListItemType.Leaf => true,
            AtkComponentTreeListItemType.LastLeafInGroup => true,
            AtkComponentTreeListItemType.CollapsibleGroupHeader => false,
            AtkComponentTreeListItemType.GroupHeader => false,
            _ => true
        };
    }

    private static string? ReadFirstNonEmptyString(StdVector<CStringPointer> values)
    {
        foreach (CStringPointer value in values)
        {
            string? text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static unsafe MenuItemCandidate[] ReadTelepotTownStringCandidates(
        AtkUnitBasePtr addonWrapper,
        AgentTelepotTownData* data)
    {
        List<string> labels = [];
        foreach (AtkValuePtr atkValue in addonWrapper.AtkValues)
        {
            object? value;
            try
            {
                value = atkValue.GetValue();
            }
            catch (NotImplementedException)
            {
                continue;
            }

            if (value is not string text || string.IsNullOrWhiteSpace(text))
                continue;

            labels.Add(text.Trim());
        }

        int maxSelectableCount = GetTelepotTownSelectableCount(data);
        return ExtractTelepotTownStringCandidates(labels, maxSelectableCount);
    }

    private static unsafe int GetTelepotTownSelectableCount(AgentTelepotTownData* data)
    {
        return data == null || data->AetheryteCount <= 0 ? int.MaxValue : data->AetheryteCount;
    }

    internal static MenuItemCandidate[] ExtractTelepotTownStringCandidates(
        IReadOnlyList<string> labels,
        int maxSelectableCount)
    {
        ArgumentNullException.ThrowIfNull(labels);

        List<MenuItemCandidate> candidates = [];
        bool skippedTitle = false;
        int selectionIndex = 0;
        foreach (string label in labels)
        {
            if (string.IsNullOrWhiteSpace(label))
                continue;

            string trimmedLabel = label.Trim();
            if (trimmedLabel.StartsWith("現在地：", StringComparison.Ordinal) ||
                trimmedLabel.StartsWith("直近の利用元：", StringComparison.Ordinal))
            {
                continue;
            }

            if (!skippedTitle)
            {
                skippedTitle = true;
                continue;
            }

            if (string.Equals(trimmedLabel, "その他", StringComparison.Ordinal))
                continue;

            if (selectionIndex >= maxSelectableCount)
                break;

            candidates.Add(new MenuItemCandidate(trimmedLabel, selectionIndex, true));
            selectionIndex++;
        }

        return [.. candidates];
    }

    internal static MenuItemMatch? TryFindMenuItemMatch(
        IReadOnlyList<MenuItemCandidate> candidates,
        string requestedLabel,
        bool containsMatch)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        string normalizedRequested = NormalizeMenuLabel(requestedLabel);
        if (string.IsNullOrWhiteSpace(normalizedRequested))
            return null;

        foreach (MenuItemCandidate candidate in candidates)
        {
            if (!candidate.IsSelectable)
                continue;

            string normalizedCandidate = NormalizeMenuLabel(candidate.Label);
            if (string.Equals(normalizedCandidate, normalizedRequested, StringComparison.OrdinalIgnoreCase))
                return new MenuItemMatch(candidate.Label, candidate.SelectionIndex);
        }

        if (!containsMatch)
            return null;

        foreach (MenuItemCandidate candidate in candidates)
        {
            if (!candidate.IsSelectable)
                continue;

            string normalizedCandidate = NormalizeMenuLabel(candidate.Label);
            if (normalizedCandidate.Contains(normalizedRequested, StringComparison.OrdinalIgnoreCase))
                return new MenuItemMatch(candidate.Label, candidate.SelectionIndex);
        }

        return null;
    }

    internal static string NormalizeMenuLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string withoutControlCharacters = RemoveControlCharacters(value);
        ReadOnlySpan<char> span = withoutControlCharacters.AsSpan().Trim();
        while (span.Length > 1 && char.IsAsciiLetterUpper(span[0]) && char.IsWhiteSpace(span[1]))
            span = span[1..].TrimStart();

        while (span.Length > 0 && IsIgnorablePrefix(span[0]))
            span = span[1..].TrimStart();

        return span.ToString();
    }

    private static string RemoveControlCharacters(string value)
    {
        if (!value.Any(char.IsControl))
            return value;

        char[] buffer = new char[value.Length];
        int length = 0;
        foreach (char character in value)
        {
            if (char.IsControl(character))
                continue;

            buffer[length] = character;
            length++;
        }

        return length == 0 ? string.Empty : new string(buffer, 0, length);
    }

    private static bool IsIgnorablePrefix(char value)
    {
        return value is ' ' or '\u3000' or '・' or '•' or '★' or '☆' or '▶' or '►' or '◆' or '◇' or '◉' or '○';
    }

    private static AtkComponentTreeListItemType? TryReadTreeListItemType(ReadOnlySpan<uint> values)
    {
        foreach (uint value in values)
        {
            if (value > (uint)AtkComponentTreeListItemType.GroupHeader)
                continue;

            return (AtkComponentTreeListItemType)value;
        }

        return null;
    }

    private static bool TryGetReadyAddon(
        IGameGui gameGui,
        string addonName,
        out string reason,
        out string summary)
    {
        AtkUnitBasePtr addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            reason = "addon_not_ready";
            summary = $"{addonName} is not ready.";
            return false;
        }

        reason = string.Empty;
        summary = string.Empty;
        return true;
    }

    private static string BuildLabelNotFoundSummary(
        string addonName,
        string requestedLabel,
        bool containsMatch,
        IReadOnlyList<MenuItemCandidate> candidates)
    {
        string[] availableLabels = candidates
            .Where(static candidate => candidate.IsSelectable)
            .Select(static candidate => NormalizeMenuLabel(candidate.Label))
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (availableLabels.Length == 0)
            return $"{addonName} did not expose any selectable menu items.";

        string modeText = containsMatch ? "substring" : "exact";
        return $"Could not find {modeText} label match for '{requestedLabel}' in {addonName}. Available items: {string.Join(", ", availableLabels)}.";
    }

    private static AddonSelectMenuItemResult CreateFailure(
        string addonName,
        string requestedLabel,
        string reason,
        string summary,
        string? matchedLabel = null,
        int? matchedIndex = null,
        string? method = null)
    {
        return new AddonSelectMenuItemResult(
            addonName,
            requestedLabel,
            matchedLabel,
            matchedIndex,
            method,
            false,
            reason,
            summary);
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName} is required.", parameterName)
            : value.Trim();
    }

    internal sealed record MenuItemCandidate(string Label, int SelectionIndex, bool IsSelectable);

    internal sealed record MenuItemMatch(string Label, int SelectionIndex);
}

[MemoryPackable]
public sealed partial record AddonSelectMenuItemResult(
    string AddonName,
    string RequestedLabel,
    string? MatchedLabel,
    int? MatchedIndex,
    string? Method,
    bool Succeeded,
    string? Reason,
    string SummaryText);
