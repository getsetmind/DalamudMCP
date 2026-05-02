using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace DalamudMCP.Plugin.Relay;

internal sealed class DalamudPluginDataRelayEndpointFactory : IPluginDataRelayEndpointFactory
{
    private readonly IDalamudPluginInterface pluginInterface;

    public DalamudPluginDataRelayEndpointFactory(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
    }

    public IPluginDataRelayEndpoint Register(string callGateName, Action<string> onData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callGateName);
        ArgumentNullException.ThrowIfNull(onData);

        ICallGateProvider<string, object> provider = pluginInterface.GetIpcProvider<string, object>(callGateName);
        provider.RegisterAction(onData);
        return new Endpoint(provider);
    }

    private sealed class Endpoint(ICallGateProvider<string, object> provider) : IPluginDataRelayEndpoint
    {
        public void Unregister()
        {
            provider.UnregisterAction();
        }
    }
}
