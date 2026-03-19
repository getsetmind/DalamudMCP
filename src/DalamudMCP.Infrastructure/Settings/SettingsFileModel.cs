namespace DalamudMCP.Infrastructure.Settings;

internal sealed record SettingsFileModel(
    string Version,
    bool ObservationProfileEnabled,
    bool ActionProfileEnabled,
    IReadOnlyList<string> EnabledTools,
    IReadOnlyList<string> EnabledResources,
    IReadOnlyList<string> EnabledAddons)
{
    public static readonly string CurrentVersion = "1";
}
