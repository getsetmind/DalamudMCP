using DalamudMCP.Infrastructure.Settings;

namespace DalamudMCP.Plugin;

public sealed record PluginRuntimeOptions(
    string WorkingDirectory,
    string PipeName,
    string SettingsFilePath,
    string AuditLogFilePath)
{
    public static PluginRuntimeOptions CreateDefault(string? workingDirectory = null, string? pipeName = null)
    {
        var storagePaths = PluginStoragePaths.Resolve("DalamudMCP", workingDirectory);
        var effectivePipeName = string.IsNullOrWhiteSpace(pipeName)
            ? $"DalamudMCP.{Environment.ProcessId}"
            : pipeName.Trim();

        return new PluginRuntimeOptions(
            storagePaths.RootDirectory,
            effectivePipeName,
            storagePaths.SettingsFilePath,
            storagePaths.AuditLogFilePath);
    }
}
