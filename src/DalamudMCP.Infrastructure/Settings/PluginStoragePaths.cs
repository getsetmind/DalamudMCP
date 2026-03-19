namespace DalamudMCP.Infrastructure.Settings;

public sealed record PluginStoragePaths(
    string RootDirectory,
    string SettingsFilePath,
    string AuditLogFilePath)
{
    public static PluginStoragePaths Resolve(string applicationName, string? overrideRootDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var rootDirectory = string.IsNullOrWhiteSpace(overrideRootDirectory)
            ? ResolveDefaultRootDirectory(applicationName.Trim())
            : Path.GetFullPath(overrideRootDirectory);

        return new PluginStoragePaths(
            rootDirectory,
            Path.Combine(rootDirectory, "settings", "policy.json"),
            Path.Combine(rootDirectory, "logs", "audit.log"));
    }

    private static string ResolveDefaultRootDirectory(string applicationName)
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LocalApplicationData could not be resolved.");
        }

        return Path.Combine(localAppData, applicationName);
    }
}
