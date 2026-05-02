namespace DalamudMCP.Plugin.Relay;

internal interface IPluginDataRelayEndpointFactory
{
    public IPluginDataRelayEndpoint Register(string callGateName, Action<string> onData);
}
