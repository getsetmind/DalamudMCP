namespace DalamudMCP.Plugin.Services;

public sealed class ScreenshotFileCleanupService : IDisposable
{
    private readonly object syncRoot = new();
    private readonly Dictionary<string, DateTimeOffset> expirations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer timer;
    private bool disposed;

    public ScreenshotFileCleanupService()
    {
        timer = new Timer(static state => ((ScreenshotFileCleanupService)state!).DeleteExpired(), this, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void Register(string filePath, DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (syncRoot)
            expirations[Path.GetFullPath(filePath)] = expiresAtUtc;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        timer.Dispose();
        DeleteExpired(deleteAll: true);
    }

    internal void DeleteExpired(bool deleteAll = false)
    {
        string[] paths;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (syncRoot)
        {
            paths = expirations
                .Where(entry => deleteAll || entry.Value <= now)
                .Select(static entry => entry.Key)
                .ToArray();
            foreach (string path in paths)
                expirations.Remove(path);
        }

        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
