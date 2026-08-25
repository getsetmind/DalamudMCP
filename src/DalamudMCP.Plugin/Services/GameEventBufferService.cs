using System.Diagnostics;
using System.Text.Json;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Services;

public sealed class GameEventBufferService
{
    public const int DefaultCapacity = 2048;
    public const int MaximumCapacity = 10000;
    public const int MaximumQueryCount = 500;

    private readonly object syncRoot = new();
    private readonly List<GameEventEntry> entries = [];
    private HashSet<string>? enabledTypes;
    private TaskCompletionSource pulse = CreatePulse();
    private long cursor;
    private long droppedCount;
    private int capacity = DefaultCapacity;

    public GameEventEntry? Publish(string type, object? data = null, string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        string normalizedType = type.Trim().ToLowerInvariant();
        string dataJson = JsonSerializer.Serialize(data, data?.GetType() ?? typeof(object), ProtocolContract.JsonOptions);

        lock (syncRoot)
        {
            if (enabledTypes is not null && !enabledTypes.Contains(normalizedType))
                return null;

            GameEventEntry entry = new(
                ++cursor,
                DateTimeOffset.UtcNow,
                normalizedType,
                string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
                dataJson);
            entries.Add(entry);
            TrimToCapacity();
            TaskCompletionSource previousPulse = pulse;
            pulse = CreatePulse();
            previousPulse.TrySetResult();
            return entry;
        }
    }

    public GameEventQueryResult Query(long afterCursor = 0, string[]? types = null, int limit = 100)
    {
        lock (syncRoot)
            return QueryCore(afterCursor, types, limit, timedOut: false);
    }

    public async ValueTask<GameEventQueryResult> WaitAsync(
        long afterCursor,
        string[]? types,
        int limit,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        TimeSpan normalizedTimeout = timeout <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(10)
            : TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 30000));
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (true)
        {
            Task notification;
            lock (syncRoot)
            {
                GameEventQueryResult available = QueryCore(afterCursor, types, limit, timedOut: false);
                if (available.Entries.Length > 0)
                    return available;
                notification = pulse.Task;
            }

            TimeSpan remaining = normalizedTimeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                lock (syncRoot)
                    return QueryCore(afterCursor, types, limit, timedOut: true);
            }

            Task delay = Task.Delay(remaining, cancellationToken);
            Task completed = await Task.WhenAny(notification, delay).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (completed == delay)
            {
                lock (syncRoot)
                    return QueryCore(afterCursor, types, limit, timedOut: true);
            }
        }
    }

    public GameEventConfiguration Configure(string[]? types, int? requestedCapacity)
    {
        lock (syncRoot)
        {
            if (requestedCapacity.HasValue)
                capacity = Math.Clamp(requestedCapacity.Value, 16, MaximumCapacity);

            enabledTypes = types is not { Length: > 0 }
                ? null
                : new HashSet<string>(
                    types.Where(static value => !string.IsNullOrWhiteSpace(value))
                        .Select(static value => value.Trim().ToLowerInvariant()),
                    StringComparer.Ordinal);
            TrimToCapacity();
            return CreateConfiguration();
        }
    }

    public GameEventConfiguration GetConfiguration()
    {
        lock (syncRoot)
            return CreateConfiguration();
    }

    private static TaskCompletionSource CreatePulse() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private GameEventQueryResult QueryCore(long afterCursor, string[]? types, int limit, bool timedOut)
    {
        int normalizedLimit = limit <= 0 ? 100 : Math.Min(limit, MaximumQueryCount);
        HashSet<string>? typeSet = types is not { Length: > 0 }
            ? null
            : new HashSet<string>(
                types.Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim().ToLowerInvariant()),
                StringComparer.Ordinal);
        GameEventEntry[] result = entries
            .Where(entry => entry.Cursor > Math.Max(0, afterCursor))
            .Where(entry => typeSet is null || typeSet.Contains(entry.Type))
            .Take(normalizedLimit)
            .ToArray();
        long nextCursor = result.Length == 0 ? Math.Max(0, afterCursor) : result[^1].Cursor;
        long oldestCursor = entries.Count == 0 ? cursor + 1 : entries[0].Cursor;
        bool truncated = entries.Any(entry =>
            entry.Cursor > nextCursor &&
            (typeSet is null || typeSet.Contains(entry.Type)));
        return new GameEventQueryResult(
            result,
            nextCursor,
            oldestCursor,
            droppedCount,
            truncated,
            timedOut,
            timedOut
                ? "No matching event arrived before the timeout."
                : $"{result.Length} event(s) returned.");
    }

    private GameEventConfiguration CreateConfiguration() =>
        new(
            enabledTypes?.OrderBy(static value => value, StringComparer.Ordinal).ToArray() ?? [],
            enabledTypes is null,
            capacity,
            entries.Count,
            droppedCount);

    private void TrimToCapacity()
    {
        int excess = entries.Count - capacity;
        if (excess <= 0)
            return;

        entries.RemoveRange(0, excess);
        droppedCount += excess;
    }
}

[MemoryPackable]
public sealed partial record GameEventEntry(
    long Cursor,
    DateTimeOffset TimestampUtc,
    string Type,
    string? CorrelationId,
    string DataJson);

[MemoryPackable]
public sealed partial record GameEventQueryResult(
    GameEventEntry[] Entries,
    long NextCursor,
    long OldestCursor,
    long DroppedCount,
    bool Truncated,
    bool TimedOut,
    string SummaryText);

[MemoryPackable]
public sealed partial record GameEventConfiguration(
    string[] EnabledTypes,
    bool AllTypesEnabled,
    int Capacity,
    int BufferedCount,
    long DroppedCount);
