using DalamudMCP.Plugin.Ipc;

namespace DalamudMCP.Plugin.Operations.Tests.TestShared.Ipc;

public sealed class FakeIpcCallGateSubscriber(bool hasFunction, object? result = null) : IPluginCallGateSubscriber
{
    public bool HasFunction { get; } = hasFunction;

    public IReadOnlyList<object?> LastArguments { get; private set; } = [];

    public object? InvokeFunc(IReadOnlyList<object?> arguments)
    {
        LastArguments = arguments.ToArray();
        return result;
    }
}
