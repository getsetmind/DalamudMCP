using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using R3;

namespace DalamudMCP.Plugin.Relay;

internal sealed class PluginDataRelayService : IPluginDataRelayService, IDisposable
{
    private readonly IPluginDataRelayEndpointFactory endpointFactory;
    private readonly Func<IReadOnlySet<string>> getInstalledPluginNames;
    private readonly IFramework? framework;
    private readonly ConcurrentDictionary<string, RelayStream> streams = new(StringComparer.OrdinalIgnoreCase);
    private int frameCounter;
    private bool disposed;

    [SupportedOSPlatform("windows")]
    public PluginDataRelayService(IDalamudPluginInterface pluginInterface, IFramework framework)
        : this(
            new DalamudPluginDataRelayEndpointFactory(pluginInterface),
            () => pluginInterface.InstalledPlugins.Select(static plugin => plugin.InternalName).ToHashSet(StringComparer.OrdinalIgnoreCase),
            framework)
    {
    }

    internal PluginDataRelayService(
        IPluginDataRelayEndpointFactory endpointFactory,
        Func<IReadOnlySet<string>> getInstalledPluginNames,
        IFramework? framework = null)
    {
        this.endpointFactory = endpointFactory ?? throw new ArgumentNullException(nameof(endpointFactory));
        this.getInstalledPluginNames = getInstalledPluginNames ?? throw new ArgumentNullException(nameof(getInstalledPluginNames));
        this.framework = framework;

        if (framework is not null)
            framework.Update += OnFrameworkUpdate;
    }

    public IReadOnlyCollection<string> ActiveChannels
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return streams.Keys.ToArray();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        if (framework is not null)
            framework.Update -= OnFrameworkUpdate;

        foreach (RelayStream stream in streams.Values)
            stream.Dispose();

        streams.Clear();
    }

    public bool Subscribe(string pluginName, string channelName, int capacity = 1000)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        string trimmedPluginName = pluginName.Trim();
        string trimmedChannelName = channelName.Trim();
        string fullChannelName = $"{trimmedPluginName}.{trimmedChannelName}";
        if (streams.ContainsKey(fullChannelName))
            return false;

        RelayStream stream = CreateStream(trimmedPluginName, trimmedChannelName, fullChannelName, capacity);
        if (streams.TryAdd(fullChannelName, stream))
            return true;

        stream.Dispose();
        return false;
    }

    public bool Unsubscribe(string fullChannelName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullChannelName);

        if (!streams.TryRemove(fullChannelName.Trim(), out RelayStream? stream))
            return false;

        stream.Dispose();
        return true;
    }

    public bool TryPoll(string fullChannelName, int? maxItems, out IReadOnlyList<string> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullChannelName);

        if (maxItems.HasValue && maxItems.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "max-items must be greater than zero.");

        if (!streams.TryGetValue(fullChannelName.Trim(), out RelayStream? stream))
        {
            data = [];
            return false;
        }

        List<string> items = [];
        while ((!maxItems.HasValue || items.Count < maxItems.Value) &&
               stream.Buffer.Reader.TryRead(out string? item))
        {
            items.Add(item);
        }

        data = items;
        return true;
    }

    public bool IsSubscribed(string fullChannelName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullChannelName);
        return streams.ContainsKey(fullChannelName.Trim());
    }

    private RelayStream CreateStream(string pluginName, string channelName, string fullChannelName, int capacity)
    {
        BoundedChannelOptions options = new(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        Channel<string> buffer = Channel.CreateBounded<string>(options);
        Subject<string> subject = new();
        IDisposable bufferSubscription = subject.Subscribe(buffer, static (value, target) => target.Writer.TryWrite(value));
        IPluginDataRelayEndpoint endpoint = endpointFactory.Register(
            $"DalamudMCP.Relay.{pluginName}.{channelName}",
            subject.OnNext);

        return new RelayStream(buffer, subject, bufferSubscription, endpoint, pluginName, fullChannelName);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (++frameCounter % 60 != 0 || streams.IsEmpty)
            return;

        IReadOnlySet<string> installedPluginNames = getInstalledPluginNames();
        foreach (KeyValuePair<string, RelayStream> entry in streams)
        {
            if (!installedPluginNames.Contains(entry.Value.PluginName))
                Unsubscribe(entry.Key);
        }
    }
}
