namespace DalamudMCP.Plugin.Readers;

public interface IPluginReaderDiagnostics
{
    public string ComponentName { get; }

    public bool IsReady { get; }

    public string Status { get; }
}
