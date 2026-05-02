namespace DalamudMCP.Plugin.Ipc;

public interface IPluginCallGateSubscriber
{
    public bool HasFunction { get; }

    public object? InvokeFunc(IReadOnlyList<object?> arguments);
}
