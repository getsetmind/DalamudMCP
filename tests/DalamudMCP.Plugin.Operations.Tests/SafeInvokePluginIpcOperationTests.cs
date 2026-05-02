using DalamudMCP.Plugin.Operations.Tests.TestShared.Ipc;
using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class SafeInvokePluginIpcOperationTests
{
    [Fact]
    public void InvokeSafeIpc_returns_success_for_convention_callgate()
    {
        SafeInvokePluginIpcResult result = SafeInvokePluginIpcOperation.InvokeSafeIpc(
            new FakeIpcGateway(("SamplePlugin.MCP.Ping", new FakeIpcCallGateSubscriber(true, true))),
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
        FakeIpcCallGateSubscriber subscriber = new(true, "ok");

        SafeInvokePluginIpcOperation.InvokeSafeIpc(
            new FakeIpcGateway(("SamplePlugin.MCP.Process", subscriber)),
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
            new FakeIpcGateway(),
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
            new FakeIpcGateway(("SamplePlugin.MCP.Ping", new FakeIpcCallGateSubscriber(false))),
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

}
