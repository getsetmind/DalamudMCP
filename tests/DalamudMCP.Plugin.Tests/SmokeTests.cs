namespace DalamudMCP.Plugin.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void PluginArtifacts_ArePresent()
    {
        var pluginAssembly = typeof(DalamudMCP.Plugin.PluginEntryPoint).Assembly;
        var outputDirectory = Path.GetDirectoryName(pluginAssembly.Location);

        Assert.False(string.IsNullOrWhiteSpace(outputDirectory));
        Assert.True(File.Exists(Path.Combine(outputDirectory!, "DalamudMCP.dll")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "DalamudMCP.json")));
    }
}
