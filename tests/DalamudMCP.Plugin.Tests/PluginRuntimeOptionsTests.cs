namespace DalamudMCP.Plugin.Tests;

public sealed class PluginRuntimeOptionsTests
{
    [Fact]
    public void CreateDefault_generates_unique_instance_scoped_pipe_name()
    {
        PluginRuntimeOptions first = PluginRuntimeOptions.CreateDefault();
        PluginRuntimeOptions second = PluginRuntimeOptions.CreateDefault();

        Assert.StartsWith($"DalamudMCP.{Environment.ProcessId}.", first.PipeName, StringComparison.Ordinal);
        Assert.StartsWith($"DalamudMCP.{Environment.ProcessId}.", second.PipeName, StringComparison.Ordinal);
        Assert.NotEqual(first.PipeName, second.PipeName);
    }

    [Fact]
    public void CreateDefault_preserves_explicit_pipe_name()
    {
        PluginRuntimeOptions options = PluginRuntimeOptions.CreateDefault(pipeName: "DalamudMCP.custom");

        Assert.Equal("DalamudMCP.custom", options.PipeName);
    }
}
