using System.Reflection;

namespace DalamudMCP.Plugin.Ipc;

internal sealed class ReflectionPluginCallGateSubscriber : IPluginCallGateSubscriber
{
    private readonly object subscriber;
    private readonly PropertyInfo hasFunctionProperty;
    private readonly MethodInfo invokeFuncMethod;

    public ReflectionPluginCallGateSubscriber(object subscriber)
    {
        this.subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        Type type = subscriber.GetType();
        hasFunctionProperty = type.GetProperty("HasFunction", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("The IPC subscriber did not expose HasFunction.");
        invokeFuncMethod = type.GetMethod("InvokeFunc", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("The IPC subscriber did not expose InvokeFunc.");
    }

    public bool HasFunction => (bool?)hasFunctionProperty.GetValue(subscriber) ?? false;

    public object? InvokeFunc(IReadOnlyList<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return invokeFuncMethod.Invoke(subscriber, arguments.ToArray());
    }
}
