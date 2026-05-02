using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class SafeInvokePluginIpcOperationTests
{
    [Fact]
    public void InvokeSafeIpc_returns_success_for_convention_callgate()
    {
        SafeInvokePluginIpcResult result = SafeInvokePluginIpcOperation.InvokeSafeIpc(
            new FakeGateway(("SamplePlugin.MCP.Ping", new FakeSubscriber(true, true))),
            new SafeInvokePluginIpcOperation.Request
            {
                PluginName = "SamplePlugin",
                Method = "Ping"
            });

        Assert.True(result.Success);
        Assert.Equal("ipc_success", result.Status);
        Assert.Equal("true", result.ReturnValue);
    }

    [Fact]
    public void InvokeSafeIpc_infers_primitive_argument_types()
    {
        FakeSubscriber subscriber = new(true, "ok");

        SafeInvokePluginIpcOperation.InvokeSafeIpc(
            new FakeGateway(("SamplePlugin.MCP.Process", subscriber)),
            new SafeInvokePluginIpcOperation.Request
            {
                PluginName = "SamplePlugin",
                Method = "Process",
                ArgumentsJson = "[42,3.5,true,\"text\",{\"k\":\"v\"}]"
            });

        Assert.Equal([42, 3.5d, true, "text", "{\"k\":\"v\"}"], subscriber.LastArguments);
    }

    [Fact]
    public void InvokeSafeIpc_returns_missing_when_callgate_is_not_available()
    {
        SafeInvokePluginIpcResult result = SafeInvokePluginIpcOperation.InvokeSafeIpc(
            new FakeGateway(),
            new SafeInvokePluginIpcOperation.Request
            {
                PluginName = "Missing",
                Method = "Ping"
            });

        Assert.False(result.Success);
        Assert.Equal("ipc_missing", result.Status);
    }

    [Fact]
    public void InvokeSafeIpc_returns_not_ready_when_subscriber_has_no_function()
    {
        SafeInvokePluginIpcResult result = SafeInvokePluginIpcOperation.InvokeSafeIpc(
            new FakeGateway(("SamplePlugin.MCP.Ping", new FakeSubscriber(false, null))),
            new SafeInvokePluginIpcOperation.Request
            {
                PluginName = "SamplePlugin",
                Method = "Ping"
            });

        Assert.False(result.Success);
        Assert.Equal("ipc_not_ready", result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_uses_injected_executor()
    {
        SafeInvokePluginIpcResult expected = new(
            "SamplePlugin",
            "Ping",
            true,
            "ipc_success",
            "true",
            null,
            "ok");
        SafeInvokePluginIpcOperation operation = new((request, _) =>
        {
            Assert.Equal("SamplePlugin", request.PluginName);
            return ValueTask.FromResult(expected);
        });

        SafeInvokePluginIpcResult actual = await operation.ExecuteAsync(
            new SafeInvokePluginIpcOperation.Request
            {
                PluginName = "SamplePlugin",
                Method = "Ping"
            },
            OperationContext.ForCli("plugin.ipc", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expected, actual);
    }

    private sealed class FakeGateway(params (string Callgate, SafeInvokePluginIpcOperation.IPluginCallGateSubscriber Subscriber)[] entries)
        : SafeInvokePluginIpcOperation.IPluginIpcGateway
    {
        private readonly Dictionary<string, SafeInvokePluginIpcOperation.IPluginCallGateSubscriber> subscribers =
            entries.ToDictionary(static entry => entry.Callgate, static entry => entry.Subscriber, StringComparer.Ordinal);

        public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out SafeInvokePluginIpcOperation.IPluginCallGateSubscriber? subscriber)
        {
            _ = typeArguments;
            return subscribers.TryGetValue(callgate, out subscriber);
        }
    }

    private sealed class FakeSubscriber(bool hasFunction, object? result) : SafeInvokePluginIpcOperation.IPluginCallGateSubscriber
    {
        public bool HasFunction { get; } = hasFunction;

        public IReadOnlyList<object?> LastArguments { get; private set; } = [];

        public object? InvokeFunc(IReadOnlyList<object?> arguments)
        {
            LastArguments = arguments.ToArray();
            return result;
        }
    }
}
