namespace DalamudMCP.Plugin.Tests;

public sealed class PluginRuntimeOptionsTests
{
    [Fact]
    public void CreateDefault_UsesProvidedWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var options = PluginRuntimeOptions.CreateDefault(workingDirectory, "DalamudMCP.TestPipe");

        Assert.Equal(Path.GetFullPath(workingDirectory), options.WorkingDirectory);
        Assert.Equal("DalamudMCP.TestPipe", options.PipeName);
        Assert.Equal(Path.Combine(Path.GetFullPath(workingDirectory), "settings", "policy.json"), options.SettingsFilePath);
        Assert.Equal(Path.Combine(Path.GetFullPath(workingDirectory), "logs", "audit.log"), options.AuditLogFilePath);
    }

    [Fact]
    public void CreateDefault_UsesLocalApplicationData_WhenWorkingDirectoryIsMissing()
    {
        var options = PluginRuntimeOptions.CreateDefault();
        var expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            "DalamudMCP");

        Assert.Equal(expectedRoot, options.WorkingDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "settings", "policy.json"), options.SettingsFilePath);
        Assert.Equal(Path.Combine(expectedRoot, "logs", "audit.log"), options.AuditLogFilePath);
    }
}
