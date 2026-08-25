using System.Text;
using Dalamud.Plugin;
using MemoryPack;

namespace DalamudMCP.Plugin.Services;

public sealed class PluginInspectionService
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;
    private readonly IDalamudPluginInterface pluginInterface;

    public PluginInspectionService(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
    }

    public PluginListResult List(string? query, string? cursor, int? requestedLimit)
    {
        if (!TryDecodeCursor(cursor, out int offset))
            return new PluginListResult(false, [], null, false, "invalid_cursor", "The cursor is invalid.");

        int limit = Math.Clamp(requestedLimit ?? DefaultLimit, 1, MaximumLimit);
        string normalizedQuery = query?.Trim() ?? string.Empty;
        PluginInfo[] matching = pluginInterface.InstalledPlugins
            .Where(plugin =>
                normalizedQuery.Length == 0 ||
                plugin.InternalName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                plugin.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static plugin => plugin.InternalName, StringComparer.OrdinalIgnoreCase)
            .Select(CreateInfo)
            .ToArray();

        if (offset > matching.Length)
            return new PluginListResult(false, [], null, false, "invalid_cursor", "The cursor is outside the result set.");

        PluginInfo[] items = matching.Skip(offset).Take(limit).ToArray();
        int nextOffset = offset + items.Length;
        bool truncated = nextOffset < matching.Length;
        return new PluginListResult(
            true,
            items,
            truncated ? EncodeCursor(nextOffset) : null,
            truncated,
            null,
            $"Returned {items.Length} of {matching.Length} installed plugins.");
    }

    public PluginDescribeResult Describe(string? pluginName)
    {
        string normalizedName = pluginName?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
            return new PluginDescribeResult(false, null, "validation_failed", "plugin-name is required.");

        IExposedPlugin? plugin = pluginInterface.InstalledPlugins.FirstOrDefault(candidate =>
            string.Equals(candidate.InternalName, normalizedName, StringComparison.OrdinalIgnoreCase));
        return plugin is null
            ? new PluginDescribeResult(false, null, "plugin_not_found", $"Plugin '{normalizedName}' was not found.")
            : new PluginDescribeResult(true, CreateInfo(plugin), null, $"Plugin details for {plugin.InternalName}.");
    }

    internal static string EncodeCursor(int offset) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"v1:{offset}"));

    internal static bool TryDecodeCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            string value = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return value.StartsWith("v1:", StringComparison.Ordinal) &&
                   int.TryParse(value.AsSpan(3), out offset) &&
                   offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static PluginInfo CreateInfo(IExposedPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var manifest = plugin.Manifest;
        return new PluginInfo(
            plugin.InternalName,
            plugin.Name,
            plugin.Version.ToString(),
            plugin.IsLoaded,
            plugin.IsOutdated,
            plugin.IsTesting,
            plugin.IsDev,
            plugin.IsThirdParty,
            plugin.IsOrphaned,
            plugin.IsDecommissioned,
            plugin.IsBanned,
            plugin.HasMainUi,
            plugin.HasConfigUi,
            manifest.Author,
            manifest.Description,
            manifest.Punchline,
            manifest.DalamudApiLevel,
            manifest.RepoUrl,
            manifest.InstalledFromUrl,
            manifest.ScheduledForDeletion,
            manifest.WorkingPluginId.ToString("D"),
            manifest.CanUnloadAsync,
            manifest.SupportsProfiles,
            manifest.Tags?.ToArray() ?? []);
    }
}

[MemoryPackable]
public sealed partial record PluginInfo(
    string InternalName,
    string Name,
    string Version,
    bool IsLoaded,
    bool IsOutdated,
    bool IsTesting,
    bool IsDev,
    bool IsThirdParty,
    bool IsOrphaned,
    bool IsDecommissioned,
    bool IsBanned,
    bool HasMainUi,
    bool HasConfigUi,
    string? Author,
    string? Description,
    string? Punchline,
    int DalamudApiLevel,
    string? RepositoryUrl,
    string? InstalledFromUrl,
    bool ScheduledForDeletion,
    string WorkingPluginId,
    bool CanUnloadAsync,
    bool SupportsProfiles,
    string[] Tags);

[MemoryPackable]
public sealed partial record PluginListResult(
    bool Success,
    PluginInfo[] Items,
    string? NextCursor,
    bool Truncated,
    string? ErrorCode,
    string SummaryText);

[MemoryPackable]
public sealed partial record PluginDescribeResult(
    bool Success,
    PluginInfo? Plugin,
    string? ErrorCode,
    string SummaryText);
