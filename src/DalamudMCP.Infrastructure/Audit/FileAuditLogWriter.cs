using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Infrastructure.Audit;

public sealed class FileAuditLogWriter : IAuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string filePath;
    private readonly long? maxFileBytes;

    public FileAuditLogWriter(string filePath, long? maxFileBytes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (maxFileBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes), "Max file bytes must be positive when specified.");
        }

        this.filePath = Path.GetFullPath(filePath);
        this.maxFileBytes = maxFileBytes;
    }

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        using var scope = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entry = new AuditLogEntry(auditEvent.Timestamp, auditEvent.EventType, auditEvent.Summary);
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await RotateIfNeededAsync(json, cancellationToken).ConfigureAwait(false);
        await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
    }

    private async Task RotateIfNeededAsync(string json, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxFileBytes is null || !File.Exists(filePath))
        {
            return;
        }

        var currentLength = new FileInfo(filePath).Length;
        var pendingLength = Encoding.UTF8.GetByteCount(json + Environment.NewLine);

        if (currentLength == 0 || currentLength + pendingLength <= maxFileBytes.Value)
        {
            return;
        }

        var archivePath = CreateArchivePath();
        await Task.Run(() => File.Move(filePath, archivePath), cancellationToken).ConfigureAwait(false);
    }

    private string CreateArchivePath()
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{extension}");
    }

    private async Task<IDisposable> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var semaphore = FileLocks.GetOrAdd(filePath, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
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
