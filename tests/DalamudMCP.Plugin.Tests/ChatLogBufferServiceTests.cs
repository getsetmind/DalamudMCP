using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.Text;
using DalamudMCP.Plugin.Services;

namespace DalamudMCP.Plugin.Tests;

public sealed class ChatLogBufferServiceTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetRecent_filters_by_channel()
    {
        ChatLogBufferService service = CreateService();
        AddEntry(service, MakeEntry(XivChatType.Say));
        AddEntry(service, MakeEntry(XivChatType.Party));
        AddEntry(service, MakeEntry(XivChatType.Shout));

        IReadOnlyList<ChatLogEntry> result = service.GetRecent(channels: [XivChatType.Say, XivChatType.Party]);

        Assert.Equal(2, result.Count);
        Assert.All(result, entry => Assert.Contains(entry.Type, new[] { XivChatType.Say, XivChatType.Party }));
    }

    [Fact]
    public void GetRecent_filters_by_timestamp()
    {
        ChatLogBufferService service = CreateService();
        AddEntry(service, MakeEntry(XivChatType.Say, BaseTime.AddMinutes(-5)));
        AddEntry(service, MakeEntry(XivChatType.Say, BaseTime));
        AddEntry(service, MakeEntry(XivChatType.Say, BaseTime.AddMinutes(5)));

        IReadOnlyList<ChatLogEntry> result = service.GetRecent(since: BaseTime);

        Assert.Equal(2, result.Count);
        Assert.All(result, entry => Assert.True(entry.Timestamp >= BaseTime));
    }

    [Fact]
    public void GetRecent_limits_and_orders_newest_first()
    {
        ChatLogBufferService service = CreateService();
        for (int index = 0; index < 10; index++)
            AddEntry(service, MakeEntry(XivChatType.Say, BaseTime.AddMinutes(index), $"message {index}"));

        IReadOnlyList<ChatLogEntry> result = service.GetRecent(maxCount: 3);

        Assert.Equal(3, result.Count);
        Assert.Equal(["message 9", "message 8", "message 7"], result.Select(static entry => entry.Message).ToArray());
    }

    [Fact]
    public void GetRecent_clamps_max_count()
    {
        ChatLogBufferService service = CreateService();
        for (int index = 0; index < 600; index++)
            AddEntry(service, MakeEntry(XivChatType.Say, BaseTime.AddMinutes(index)));

        IReadOnlyList<ChatLogEntry> result = service.GetRecent(maxCount: 999);

        Assert.Equal(500, result.Count);
    }

    private static ChatLogBufferService CreateService()
    {
        ChatLogBufferService service = (ChatLogBufferService)RuntimeHelpers.GetUninitializedObject(typeof(ChatLogBufferService));
        typeof(ChatLogBufferService)
            .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new ConcurrentQueue<ChatLogEntry>());
        return service;
    }

    private static ChatLogEntry MakeEntry(
        XivChatType type,
        DateTimeOffset? timestamp = null,
        string message = "test")
    {
        return new ChatLogEntry(
            Guid.NewGuid(),
            timestamp ?? BaseTime,
            type,
            type.ToString(),
            0,
            null,
            message,
            XivChatRelationKind.None,
            XivChatRelationKind.None);
    }

    private static void AddEntry(ChatLogBufferService service, ChatLogEntry entry)
    {
        ConcurrentQueue<ChatLogEntry> queue = (ConcurrentQueue<ChatLogEntry>)typeof(ChatLogBufferService)
            .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        queue.Enqueue(entry);
    }
}
