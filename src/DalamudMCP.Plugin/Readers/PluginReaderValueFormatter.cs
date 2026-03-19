using System.Globalization;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

internal static class PluginReaderValueFormatter
{
    public static string FormatDutyName(int? territoryId) =>
        territoryId is null ? "Unknown duty" : $"Territory#{territoryId.Value.ToString(CultureInfo.InvariantCulture)}";

    public static string FormatDutySummary(bool inDuty, string dutyName, int? territoryId, bool isDutyComplete)
    {
        if (!inDuty)
        {
            return territoryId is null
                ? "Not currently in duty."
                : $"Not currently in duty; current territory is {territoryId.Value.ToString(CultureInfo.InvariantCulture)}.";
        }

        var completionText = isDutyComplete ? " Duty completion has been detected." : string.Empty;
        return $"{dutyName} is active.{completionText}";
    }

    public static string FormatAddonSummary(string displayName, bool isReady, bool isVisible)
    {
        var visibility = isVisible ? "visible" : "hidden";
        return isReady
            ? $"{displayName} is open and {visibility}."
            : $"{displayName} is not currently open.";
    }

    public static NodeSnapshot CreateAddonRootNode(
        int nodeId,
        string addonName,
        bool visible,
        float x,
        float y,
        float width,
        float height) =>
        new(
            NodeId: nodeId,
            NodeType: "addon",
            Visible: visible,
            X: x,
            Y: y,
            Width: width,
            Height: height,
            Text: addonName,
            Children: []);

    public static IReadOnlyList<StringTableEntry> CreateStringEntries(IEnumerable<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var entries = new List<StringTableEntry>();
        var index = 0;
        foreach (var value in values)
        {
            var formatted = FormatValue(value);
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                entries.Add(new StringTableEntry(index, formatted, formatted));
            }

            index++;
        }

        return entries;
    }

    public static IReadOnlyDictionary<string, int> CreateInventoryCategoryCounts(
        int mainInventoryCount,
        int equippedCount,
        int armoryCount,
        int currencyEntryCount,
        int crystalStackCount) =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["main_inventory"] = mainInventoryCount,
            ["equipped"] = equippedCount,
            ["armory"] = armoryCount,
            ["currency_entries"] = currencyEntryCount,
            ["crystal_stacks"] = crystalStackCount,
        };

    public static string FormatInventorySummary(
        int occupiedSlots,
        int totalSlots,
        int gil,
        int equippedCount,
        int armoryCount,
        int currencyEntryCount,
        int crystalStackCount)
    {
        var gilText = gil > 0
            ? $"{gil.ToString(CultureInfo.InvariantCulture)} gil tracked"
            : "gil unavailable";

        return
            $"{occupiedSlots.ToString(CultureInfo.InvariantCulture)}/{totalSlots.ToString(CultureInfo.InvariantCulture)} main inventory slots occupied; " +
            $"{armoryCount.ToString(CultureInfo.InvariantCulture)} armory items, " +
            $"{equippedCount.ToString(CultureInfo.InvariantCulture)} equipped items, " +
            $"{currencyEntryCount.ToString(CultureInfo.InvariantCulture)} currency entries, and " +
            $"{crystalStackCount.ToString(CultureInfo.InvariantCulture)} crystal stacks tracked ({gilText}).";
    }

    public static string? FormatValue(object? value) =>
        value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text,
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
}
