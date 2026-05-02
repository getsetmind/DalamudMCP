namespace DalamudMCP.Plugin.Relay;

public interface IPluginDataRelayService
{
    public bool Subscribe(string pluginName, string channelName, int capacity = 1000);

    public bool Unsubscribe(string fullChannelName);

    public bool TryPoll(string fullChannelName, int? maxItems, out IReadOnlyList<string> data);

    public bool IsSubscribed(string fullChannelName);

    public IReadOnlyCollection<string> ActiveChannels { get; }
}
