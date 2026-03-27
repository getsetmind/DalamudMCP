namespace DalamudMCP.Plugin;

public sealed class PluginRuntimeOptions
{
    private const string PipePrefix = "DalamudMCP";

    public PluginRuntimeOptions(string pipeName, string? workingDirectory = null)
    {
        PipeName = string.IsNullOrWhiteSpace(pipeName)
            ? throw new ArgumentException("Pipe name must be non-empty.", nameof(pipeName))
            : pipeName.Trim();
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? AppContext.BaseDirectory
            : workingDirectory.Trim();
        CaptureDirectoryPath = Path.Combine(WorkingDirectory, "captures");
    }

    public string PipeName { get; }

    public string WorkingDirectory { get; }

    public string CaptureDirectoryPath { get; }

    public static PluginRuntimeOptions CreateDefault(string? workingDirectory = null, string? pipeName = null)
    {
        return new PluginRuntimeOptions(pipeName ?? CreateDefaultPipeName(), workingDirectory);
    }

    private static string CreateDefaultPipeName()
    {
        string instanceId = Guid.NewGuid().ToString("N")[..8];
        return $"{PipePrefix}.{Environment.ProcessId}.{instanceId}";
    }
}
