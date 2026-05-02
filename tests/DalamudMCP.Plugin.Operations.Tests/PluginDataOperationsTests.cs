using DalamudMCP.Plugin.Relay;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class PluginDataOperationsTests
{
    [Fact]
    public void Subscribe_returns_success_for_new_channel()
    {
        FakeRelay relay = new();

        PluginDataSubscribeResult result = PluginDataSubscribeOperation.ExecuteSubscribe(
            relay,
            new PluginDataSubscribeOperation.Request
            {
                PluginName = "SamplePlugin",
                Channel = "status"
            });

        Assert.True(result.Success);
        Assert.Equal("subscribe_success", result.Status);
        Assert.Equal("SamplePlugin.status", result.FullChannelName);
    }

    [Fact]
    public void Poll_returns_channel_not_found_for_missing_channel()
    {
        FakeRelay relay = new();

        PluginDataPollResult result = PluginDataPollOperation.ExecutePoll(
            relay,
            new PluginDataPollOperation.Request
            {
                Channel = "Missing.status"
            });

        Assert.True(result.Success);
        Assert.Equal("channel_not_found", result.Status);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Poll_returns_buffered_items()
    {
        FakeRelay relay = new();
        relay.Subscribe("SamplePlugin", "status");
        relay.Push("SamplePlugin.status", "one");
        relay.Push("SamplePlugin.status", "two");

        PluginDataPollResult result = PluginDataPollOperation.ExecutePoll(
            relay,
            new PluginDataPollOperation.Request
            {
                Channel = "SamplePlugin.status",
                MaxItems = 1
            });

        Assert.True(result.Success);
        Assert.Equal("data_available", result.Status);
        Assert.Equal(["one"], result.Items);

        relay.TryPoll("SamplePlugin.status", null, out IReadOnlyList<string> remaining);
        Assert.Equal(["two"], remaining);
    }

    [Fact]
    public void Poll_returns_validation_failed_for_invalid_max_items()
    {
        FakeRelay relay = new();
        relay.Subscribe("SamplePlugin", "status");

        PluginDataPollResult result = PluginDataPollOperation.ExecutePoll(
            relay,
            new PluginDataPollOperation.Request
            {
                Channel = "SamplePlugin.status",
                MaxItems = 0
            });

        Assert.False(result.Success);
        Assert.Equal("validation_failed", result.Status);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Unsubscribe_returns_not_subscribed_for_missing_channel()
    {
        FakeRelay relay = new();

        PluginDataUnsubscribeResult result = PluginDataUnsubscribeOperation.ExecuteUnsubscribe(
            relay,
            new PluginDataUnsubscribeOperation.Request
            {
                Channel = "Missing.status"
            });

        Assert.True(result.Success);
        Assert.Equal("not_subscribed", result.Status);
    }

    private sealed class FakeRelay : IPluginDataRelayService
    {
        private readonly Dictionary<string, Queue<string>> channels = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> ActiveChannels => channels.Keys.ToArray();

        public bool Subscribe(string pluginName, string channelName, int capacity = 1000)
        {
            _ = capacity;
            return channels.TryAdd($"{pluginName}.{channelName}", new Queue<string>());
        }

        public bool Unsubscribe(string fullChannelName)
        {
            return channels.Remove(fullChannelName);
        }

        public bool TryPoll(string fullChannelName, int? maxItems, out IReadOnlyList<string> data)
        {
            if (!channels.TryGetValue(fullChannelName, out Queue<string>? channel))
            {
                data = [];
                return false;
            }

            List<string> items = [];
            while (channel.Count > 0 && (!maxItems.HasValue || items.Count < maxItems.Value))
                items.Add(channel.Dequeue());

            data = items;
            return true;
        }

        public bool IsSubscribed(string fullChannelName)
        {
            return channels.ContainsKey(fullChannelName);
        }

        public void Push(string fullChannelName, string json)
        {
            channels[fullChannelName].Enqueue(json);
        }
    }
}
