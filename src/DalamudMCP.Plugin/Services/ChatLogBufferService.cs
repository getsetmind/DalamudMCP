using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using MemoryPack;

namespace DalamudMCP.Plugin.Services;

[MemoryPackable]
public sealed partial record ChatLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    XivChatType Type,
    string ChannelName,
    uint SenderId,
    string? SenderName,
    string Message,
    XivChatRelationKind SourceKind,
    XivChatRelationKind TargetKind);

[SupportedOSPlatform("windows")]
public sealed class ChatLogBufferService : IDisposable
{
    private const int DefaultCapacity = 1000;
    private const int DefaultMaxCount = 100;
    private const int MaxAllowedMaxCount = 500;

    private readonly IChatGui chatGui;
    private readonly ConcurrentQueue<ChatLogEntry> entries = new();
    private readonly int maxCapacity;
    private bool disposed;

    public ChatLogBufferService(IChatGui chatGui, int maxCapacity = DefaultCapacity)
    {
        this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
        this.maxCapacity = maxCapacity > 0 ? maxCapacity : DefaultCapacity;
        chatGui.ChatMessage += OnChatMessage;
    }

    public int Count => entries.Count;

    public IReadOnlyList<ChatLogEntry> GetRecent(
        XivChatType[]? channels = null,
        DateTimeOffset? since = null,
        int maxCount = DefaultMaxCount)
    {
        int normalizedMaxCount = maxCount <= 0
            ? DefaultMaxCount
            : Math.Min(maxCount, MaxAllowedMaxCount);

        IEnumerable<ChatLogEntry> query = entries.ToArray();

        if (channels is { Length: > 0 })
        {
            HashSet<XivChatType> channelSet = new(channels);
            query = query.Where(entry => channelSet.Contains(entry.Type));
        }

        if (since.HasValue)
            query = query.Where(entry => entry.Timestamp >= since.Value);

        return query
            .OrderByDescending(static entry => entry.Timestamp)
            .Take(normalizedMaxCount)
            .ToArray();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        chatGui.ChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        ChatLogEntry entry = new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            message.LogKind,
            message.LogKind.ToString(),
            0,
            message.Sender?.TextValue,
            message.Message?.TextValue ?? string.Empty,
            message.SourceKind,
            message.TargetKind);

        entries.Enqueue(entry);
        while (entries.Count > maxCapacity)
            entries.TryDequeue(out _);
    }
}
