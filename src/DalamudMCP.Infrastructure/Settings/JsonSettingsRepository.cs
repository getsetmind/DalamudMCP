using System.Collections.Concurrent;
using System.Text.Json;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Infrastructure.Settings;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string filePath;

    public JsonSettingsRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = Path.GetFullPath(filePath);
    }

    public async Task<ExposurePolicy> LoadAsync(CancellationToken cancellationToken)
    {
        BridgeTrace.Write($"settings.load.enter path={filePath}");
        using var scope = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        BridgeTrace.Write($"settings.load.locked path={filePath}");
        if (!File.Exists(filePath))
        {
            BridgeTrace.Write($"settings.load.default_missing path={filePath}");
            return ExposurePolicy.Default;
        }

        try
        {
            await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var policy = await SettingsFileModelSerializer.DeserializePolicyAsync(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            BridgeTrace.Write($"settings.load.success path={filePath}");
            return policy;
        }
        catch (JsonException)
        {
            await BackupCorruptFileAsync(cancellationToken).ConfigureAwait(false);
            BridgeTrace.Write($"settings.load.corrupt_default path={filePath}");
            return ExposurePolicy.Default;
        }
    }

    public async Task SaveAsync(ExposurePolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var scope = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var model = SettingsFileModelSerializer.FromPolicy(policy);
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, model, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<IDisposable> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var semaphore = FileLocks.GetOrAdd(filePath, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    private Task BackupCorruptFileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

        var backupPath = $"{filePath}.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
        File.Move(filePath, backupPath);
        return Task.CompletedTask;
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private bool disposed;

        public Releaser(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            semaphore.Release();
            disposed = true;
        }
    }
}
