using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using DalamudMCP.Plugin.Ipc;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class PluginIpcGatewayTests
{
    [Fact]
    public void Constructor_rejects_null_plugin_interface()
    {
        Assert.Throws<ArgumentNullException>(() => new PluginIpcGateway(null!));
    }

    [Fact]
    public void TryCreate_rejects_invalid_inputs()
    {
        PluginIpcGateway gateway = new(CreatePluginInterfaceProxy("Test.Callgate", new BoolCallGateSubscriber(true)));

        Assert.Throws<ArgumentException>(() => gateway.TryCreate(" ", [typeof(bool)], out _));
        Assert.Throws<ArgumentNullException>(() => gateway.TryCreate("Test.Callgate", null!, out _));
    }

    [Fact]
    public void TryCreate_returns_false_when_callgate_is_unavailable()
    {
        PluginIpcGateway gateway = new(CreatePluginInterfaceProxy("Other.Callgate", new BoolCallGateSubscriber(true)));

        bool created = gateway.TryCreate("Missing.Callgate", [typeof(bool)], out IPluginCallGateSubscriber? subscriber);

        Assert.False(created);
        Assert.Null(subscriber);
    }

    [Fact]
    public void TryCreate_returns_wrapped_subscriber_for_matching_callgate()
    {
        PluginIpcGateway gateway = new(CreatePluginInterfaceProxy("Test.Callgate", new BoolCallGateSubscriber(true)));

        bool created = gateway.TryCreate("Test.Callgate", [typeof(bool)], out IPluginCallGateSubscriber? subscriber);

        Assert.True(created);
        Assert.NotNull(subscriber);
        Assert.True(subscriber.HasFunction);
        Assert.Equal(true, subscriber.InvokeFunc([]));
    }

    [Fact]
    public void TryCreate_returns_false_when_no_generic_subscriber_shape_matches()
    {
        PluginIpcGateway gateway = new(CreatePluginInterfaceProxy("Test.Callgate", new BoolCallGateSubscriber(true)));
        Type[] unsupportedTypeArguments = Enumerable.Repeat(typeof(string), 10).ToArray();

        bool created = gateway.TryCreate("Test.Callgate", unsupportedTypeArguments, out IPluginCallGateSubscriber? subscriber);

        Assert.False(created);
        Assert.Null(subscriber);
    }

    private static IDalamudPluginInterface CreatePluginInterfaceProxy(string callgate, object? subscriber)
    {
        IDalamudPluginInterface proxy = DispatchProxy.Create<IDalamudPluginInterface, PluginInterfaceProxy>();
        PluginInterfaceProxy implementation = (PluginInterfaceProxy)(object)proxy;
        implementation.Callgate = callgate;
        implementation.Subscriber = subscriber;
        return proxy;
    }

#pragma warning disable CA1852 // DispatchProxy requires the proxy base type to be non-sealed.
    private class PluginInterfaceProxy : DispatchProxy
#pragma warning restore CA1852
    {
        public string Callgate { get; set; } = string.Empty;

        public object? Subscriber { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is not null &&
                string.Equals(targetMethod.Name, "GetIpcSubscriber", StringComparison.Ordinal) &&
                args is [{ } requestedCallgate] &&
                string.Equals((string)requestedCallgate, Callgate, StringComparison.Ordinal))
            {
                return Subscriber;
            }

            return GetDefaultValue(targetMethod?.ReturnType);
        }

        private static object? GetDefaultValue(Type? type)
        {
            return type is { IsValueType: true }
                ? Activator.CreateInstance(type)
                : null;
        }
    }

    private sealed class BoolCallGateSubscriber(bool result) : ICallGateSubscriber<bool>
    {
        private readonly bool result = result;

        public bool HasAction => true;

        public bool HasFunction => true;

        public void Subscribe(Action action)
        {
            _ = action;
        }

        public void Unsubscribe(Action action)
        {
            _ = action;
        }

        public void InvokeAction()
        {
        }

        public bool InvokeFunc()
        {
            return result;
        }
    }
}
