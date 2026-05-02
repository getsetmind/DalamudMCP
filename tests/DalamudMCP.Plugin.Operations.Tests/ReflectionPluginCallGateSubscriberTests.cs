using DalamudMCP.Plugin.Ipc;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class ReflectionPluginCallGateSubscriberTests
{
    [Fact]
    public void HasFunction_returns_subscriber_actual_value()
    {
        ReflectionPluginCallGateSubscriber ready = new(new DummySubscriber { HasFunction = true });
        ReflectionPluginCallGateSubscriber missing = new(new DummySubscriber { HasFunction = false });

        Assert.True(ready.HasFunction);
        Assert.False(missing.HasFunction);
    }

    [Fact]
    public void InvokeFunc_calls_subscriber_and_returns_result()
    {
        ReflectionPluginCallGateSubscriber subscriber = new(new DummySubscriber());

        object? result = subscriber.InvokeFunc(["alpha", 123]);

        Assert.Equal("alpha:123", result);
    }

    [Fact]
    public void Constructor_rejects_invalid_subscriber_shapes()
    {
        Assert.Throws<ArgumentNullException>(() => new ReflectionPluginCallGateSubscriber(null!));
        Assert.Throws<InvalidOperationException>(() => new ReflectionPluginCallGateSubscriber(new SubscriberWithoutHasFunction()));
        Assert.Throws<InvalidOperationException>(() => new ReflectionPluginCallGateSubscriber(new SubscriberWithoutInvokeFunc()));
    }

    [Fact]
    public void InvokeFunc_rejects_null_arguments()
    {
        ReflectionPluginCallGateSubscriber subscriber = new(new DummySubscriber());

        Assert.Throws<ArgumentNullException>(() => subscriber.InvokeFunc(null!));
    }

    private sealed class DummySubscriber
    {
        private readonly string separator = ":";

        public bool HasFunction { get; init; } = true;

        public string InvokeFunc(string name, int count)
        {
            return $"{name}{separator}{count}";
        }
    }

    private sealed class SubscriberWithoutHasFunction
    {
        private readonly string result = string.Empty;

        public string InvokeFunc(params object?[] args)
        {
            _ = args;
            return result;
        }
    }

    private sealed class SubscriberWithoutInvokeFunc
    {
        public bool HasFunction { get; init; }
    }
}
