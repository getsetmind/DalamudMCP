using System.Threading.Channels;
using R3;

namespace DalamudMCP.Plugin.Relay;

internal sealed class RelayStream : IDisposable
{
    private bool disposed;

    public RelayStream(
        Channel<string> buffer,
        Subject<string> subject,
        IDisposable bufferSubscription,
        IPluginDataRelayEndpoint endpoint,
        string pluginName,
        string fullChannelName)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        BufferSubscription = bufferSubscription ?? throw new ArgumentNullException(nameof(bufferSubscription));
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        PluginName = string.IsNullOrWhiteSpace(pluginName) ? throw new ArgumentException("Plugin name is required.", nameof(pluginName)) : pluginName;
        FullChannelName = string.IsNullOrWhiteSpace(fullChannelName) ? throw new ArgumentException("Channel name is required.", nameof(fullChannelName)) : fullChannelName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Channel<string> Buffer { get; }

    public Subject<string> Subject { get; }

    public IDisposable BufferSubscription { get; }

    public IPluginDataRelayEndpoint Endpoint { get; }

    public string PluginName { get; }

    public string FullChannelName { get; }

    public DateTimeOffset CreatedAt { get; }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Endpoint.Unregister();
        BufferSubscription.Dispose();
        Subject.Dispose();
        Buffer.Writer.TryComplete();
    }
}
