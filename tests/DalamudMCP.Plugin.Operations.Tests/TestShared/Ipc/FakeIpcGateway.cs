using DalamudMCP.Plugin.Ipc;

namespace DalamudMCP.Plugin.Operations.Tests.TestShared.Ipc;

public sealed class FakeIpcGateway(params (string Callgate, IPluginCallGateSubscriber Subscriber)[] entries) : IPluginIpcGateway
{
    private readonly Dictionary<string, IPluginCallGateSubscriber> subscribers =
        entries.ToDictionary(static entry => entry.Callgate, static entry => entry.Subscriber, StringComparer.Ordinal);

    public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out IPluginCallGateSubscriber? subscriber)
    {
        _ = typeArguments;
        return subscribers.TryGetValue(callgate, out subscriber);
    }
}
