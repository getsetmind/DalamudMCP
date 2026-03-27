using DalamudMCP.Framework;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class UnsafeInvokePluginIpcOperationTests
{
    [Fact]
    public void ParseKindsCsv_parses_supported_value_kinds()
    {
        PluginIpcValueKind[] kinds = UnsafeInvokePluginIpcOperation.ParseKindsCsv("string,bool,int");

        Assert.Equal([PluginIpcValueKind.Text, PluginIpcValueKind.Bool, PluginIpcValueKind.Whole32], kinds);
    }

    [Fact]
    public void InvokeUnsafeIpc_returns_missing_when_callgate_is_unavailable()
    {
        UnsafeInvokePluginIpcResult result = UnsafeInvokePluginIpcOperation.InvokeUnsafeIpc(
            new FakeGateway(),
            new UnsafeInvokePluginIpcOperation.Request
            {
                Callgate = "Missing.Plugin.Call",
                ResultKind = "bool"
            });

        Assert.False(result.Succeeded);
        Assert.Equal("ipc_missing", result.Reason);
    }

    [Fact]
    public void InvokeUnsafeIpc_returns_serialized_result_when_call_succeeds()
    {
        UnsafeInvokePluginIpcResult result = UnsafeInvokePluginIpcOperation.InvokeUnsafeIpc(
            new FakeGateway(("MyPlugin.Test", new FakeSubscriber(true, true))),
            new UnsafeInvokePluginIpcOperation.Request
            {
                Callgate = "MyPlugin.Test",
                ResultKind = "bool"
            });

        Assert.True(result.Succeeded);
        Assert.Equal("true", result.ResultJson);
        Assert.Equal("IPC 'MyPlugin.Test' returned true.", result.SummaryText);
    }

    [Fact]
    public async Task ExecuteAsync_uses_injected_executor()
    {
        UnsafeInvokePluginIpcResult expected = new(
            "MyPlugin.Test",
            true,
            null,
            "bool",
            "true",
            "IPC 'MyPlugin.Test' returned true.");
        UnsafeInvokePluginIpcOperation operation = new((request, _) =>
        {
            Assert.Equal("MyPlugin.Test", request.Callgate);
            return ValueTask.FromResult(expected);
        });

        UnsafeInvokePluginIpcResult actual = await operation.ExecuteAsync(
            new UnsafeInvokePluginIpcOperation.Request
            {
                Callgate = "MyPlugin.Test",
                ResultKind = "bool"
            },
            OperationContext.ForCli("unsafe.invoke.plugin-ipc", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expected, actual);
    }

    private sealed class FakeGateway(params (string Callgate, UnsafeInvokePluginIpcOperation.IPluginCallGateSubscriber Subscriber)[] entries)
        : UnsafeInvokePluginIpcOperation.IPluginIpcGateway
    {
        private readonly Dictionary<string, UnsafeInvokePluginIpcOperation.IPluginCallGateSubscriber> subscribers =
            entries.ToDictionary(static entry => entry.Callgate, static entry => entry.Subscriber, StringComparer.Ordinal);

        public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out UnsafeInvokePluginIpcOperation.IPluginCallGateSubscriber? subscriber)
        {
            _ = typeArguments;
            return subscribers.TryGetValue(callgate, out subscriber);
        }
    }

    private sealed class FakeSubscriber(bool hasFunction, object? result) : UnsafeInvokePluginIpcOperation.IPluginCallGateSubscriber
    {
        public bool HasFunction { get; } = hasFunction;

        public object? InvokeFunc(IReadOnlyList<object?> arguments)
        {
            _ = arguments;
            return result;
        }
    }
}
