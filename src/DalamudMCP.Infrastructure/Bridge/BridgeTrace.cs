namespace DalamudMCP.Infrastructure.Bridge;

internal static class BridgeTrace
{
    private static readonly object SyncRoot = new();
    private static readonly string TraceFilePath =
        Path.Combine(Path.GetTempPath(), "DalamudMCP.bridge.trace.log");

    public static void Write(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O} {message}";
            lock (SyncRoot)
            {
                File.AppendAllText(TraceFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
