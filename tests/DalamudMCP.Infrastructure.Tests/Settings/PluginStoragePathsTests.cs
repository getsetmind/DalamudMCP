using DalamudMCP.Infrastructure.Settings;

namespace DalamudMCP.Infrastructure.Tests.Settings;

public sealed class PluginStoragePathsTests
{
    [Fact]
    public void Resolve_UsesOverrideDirectory_WhenProvided()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var paths = PluginStoragePaths.Resolve("DalamudMCP", root);

        Assert.Equal(Path.GetFullPath(root), paths.RootDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "settings", "policy.json"), paths.SettingsFilePath);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "logs", "audit.log"), paths.AuditLogFilePath);
    }

    [Fact]
    public void Resolve_UsesLocalApplicationData_WhenOverrideIsMissing()
    {
        var paths = PluginStoragePaths.Resolve("DalamudMCP.Tests");

        Assert.Contains(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                "DalamudMCP.Tests"),
            paths.RootDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("settings", "policy.json"), paths.SettingsFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("logs", "audit.log"), paths.AuditLogFilePath, StringComparison.OrdinalIgnoreCase);
    }
}
