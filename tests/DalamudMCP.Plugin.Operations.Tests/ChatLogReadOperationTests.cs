using Dalamud.Game.Text;
using DalamudMCP.Plugin.Services;
using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class ChatLogReadOperationTests
{
    [Fact]
    public void ParseChannels_returns_matching_chat_types()
    {
        XivChatType[]? channels = ChatLogReadOperation.ParseChannels(["Say", "party", "not-a-channel"]);

        Assert.NotNull(channels);
        Assert.Equal([XivChatType.Say, XivChatType.Party], channels);
    }

    [Fact]
    public async Task ExecuteAsync_uses_injected_executor()
    {
        ChatLogSnapshot expected = new(
            DateTimeOffset.UtcNow,
            [
                new ChatLogEntry(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    XivChatType.Say,
                    "Say",
                    0,
                    "Tester",
                    "Hello",
                    XivChatRelationKind.None,
                    XivChatRelationKind.None)
            ],
            1,
            "1 log entries returned.");
        ChatLogReadOperation operation = new((request, _) =>
        {
            Assert.NotNull(request.Channels);
            Assert.Equal(["Say"], request.Channels!);
            return ValueTask.FromResult(expected);
        });

        ChatLogSnapshot actual = await operation.ExecuteAsync(
            new ChatLogReadOperation.Request { Channels = ["Say"] },
            OperationContext.ForCli("chat.read", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reader_status_reflects_constructor_values()
    {
        ChatLogReadOperation operation = new(
            (_, _) => ValueTask.FromResult(new ChatLogSnapshot(DateTimeOffset.UtcNow, [], 0, "empty")),
            isReady: false,
            detail: "not_initialized");

        Assert.Equal("chat.read", operation.ReaderKey);
        Assert.False(operation.IsReady);
        Assert.Equal("not_initialized", operation.Detail);
    }
}
