using DalamudMCP.Plugin.Relay;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginDataRelayServiceTests
{
    [Fact]
    public void Subscribe_registers_expected_ipc_callgate()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        bool subscribed = service.Subscribe("SamplePlugin", "status");

        Assert.True(subscribed);
        Assert.True(service.IsSubscribed("SamplePlugin.status"));
        Assert.Equal("DalamudMCP.Relay.SamplePlugin.status", factory.LastCallGateName);
    }

    [Fact]
    public void TryPoll_reads_only_max_items_and_keeps_remaining_items()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        service.Subscribe("SamplePlugin", "status");

        factory.Push("one");
        factory.Push("two");
        factory.Push("three");

        bool firstFound = service.TryPoll("SamplePlugin.status", 2, out IReadOnlyList<string> first);
        bool secondFound = service.TryPoll("SamplePlugin.status", null, out IReadOnlyList<string> second);

        Assert.True(firstFound);
        Assert.Equal(["one", "two"], first);
        Assert.True(secondFound);
        Assert.Equal(["three"], second);
    }

    [Fact]
    public void TryPoll_returns_false_for_missing_channel()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        bool found = service.TryPoll("Missing.status", null, out IReadOnlyList<string> data);

        Assert.False(found);
        Assert.Empty(data);
    }

    [Fact]
    public void Bounded_buffer_drops_oldest_items()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        service.Subscribe("SamplePlugin", "status", capacity: 2);

        factory.Push("one");
        factory.Push("two");
        factory.Push("three");

        service.TryPoll("SamplePlugin.status", null, out IReadOnlyList<string> data);

        Assert.Equal(["two", "three"], data);
    }

    [Fact]
    public void Unsubscribe_unregisters_endpoint_and_removes_channel()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        service.Subscribe("SamplePlugin", "status");

        bool unsubscribed = service.Unsubscribe("SamplePlugin.status");

        Assert.True(unsubscribed);
        Assert.False(service.IsSubscribed("SamplePlugin.status"));
        Assert.True(factory.LastEndpoint?.Unregistered);
    }

    [Fact]
    public void Subscribe_returns_false_and_does_not_register_duplicate_endpoint()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        bool first = service.Subscribe("SamplePlugin", "status");
        bool second = service.Subscribe("SamplePlugin", "status");

        Assert.True(first);
        Assert.False(second);
        Assert.Single(factory.Endpoints);
    }

    [Fact]
    public void Dispose_unregisters_all_endpoints()
    {
        FakeEndpointFactory factory = new();
        PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        service.Subscribe("SamplePlugin", "status");
        service.Subscribe("OtherPlugin", "events");

        service.Dispose();

        Assert.All(factory.Endpoints, static endpoint => Assert.True(endpoint.Unregistered));
    }

    [Fact]
    public void Unsubscribe_removes_channel_so_later_poll_returns_false()
    {
        FakeEndpointFactory factory = new();
        using PluginDataRelayService service = new(factory, static () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        service.Subscribe("SamplePlugin", "status");
        factory.Push("one");

        service.Unsubscribe("SamplePlugin.status");
        bool found = service.TryPoll("SamplePlugin.status", null, out IReadOnlyList<string> data);

        Assert.False(found);
        Assert.Empty(data);
    }

    private sealed class FakeEndpointFactory : IPluginDataRelayEndpointFactory
    {
        private Action<string>? onData;

        public string? LastCallGateName { get; private set; }

        public FakeEndpoint? LastEndpoint { get; private set; }

        public List<FakeEndpoint> Endpoints { get; } = [];

        public IPluginDataRelayEndpoint Register(string callGateName, Action<string> onData)
        {
            LastCallGateName = callGateName;
            this.onData = onData;
            LastEndpoint = new FakeEndpoint();
            Endpoints.Add(LastEndpoint);
            return LastEndpoint;
        }

        public void Push(string json)
        {
            Assert.NotNull(onData);
            onData(json);
        }
    }

    private sealed class FakeEndpoint : IPluginDataRelayEndpoint
    {
        public bool Unregistered { get; private set; }

        public void Unregister()
        {
            Unregistered = true;
        }
    }
}
