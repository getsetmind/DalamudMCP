namespace DalamudMCP.Plugin.Ipc;

public interface IPluginIpcGateway
{
    public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out IPluginCallGateSubscriber? subscriber);
}
