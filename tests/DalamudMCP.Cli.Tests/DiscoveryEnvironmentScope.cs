using DalamudMCP.Protocol;

namespace DalamudMCP.Cli.Tests;

internal sealed class DiscoveryEnvironmentScope : IDisposable
{
    private readonly string? previousConfigDirectory;
    private readonly string? previousPipeName;

    public DiscoveryEnvironmentScope()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "DalamudMCP.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);

        previousConfigDirectory = Environment.GetEnvironmentVariable(ProtocolClientDiscovery.ConfigDirectoryEnvironmentVariableName);
        previousPipeName = Environment.GetEnvironmentVariable("DALAMUD_MCP_PIPE");

        Environment.SetEnvironmentVariable(ProtocolClientDiscovery.ConfigDirectoryEnvironmentVariableName, DirectoryPath);
        Environment.SetEnvironmentVariable("DALAMUD_MCP_PIPE", null);
    }

    public string DirectoryPath { get; }

    public void WriteDiscovery(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        ProtocolClientDiscovery.Write(
            new ProtocolClientDiscoveryRecord(pipeName, Environment.ProcessId, DateTimeOffset.UtcNow),
            DirectoryPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ProtocolClientDiscovery.ConfigDirectoryEnvironmentVariableName, previousConfigDirectory);
        Environment.SetEnvironmentVariable("DALAMUD_MCP_PIPE", previousPipeName);

        if (Directory.Exists(DirectoryPath))
            Directory.Delete(DirectoryPath, recursive: true);
    }
}
