namespace DalamudMCP.Plugin.Readers;

public interface IPluginReaderStatus
{
    public string ReaderKey { get; }

    public bool IsReady { get; }

    public string Detail { get; }
}
