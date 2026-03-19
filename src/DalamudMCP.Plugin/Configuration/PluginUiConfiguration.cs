using Dalamud.Configuration;

namespace DalamudMCP.Plugin.Configuration;

public sealed class PluginUiConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoLaunchHttpServerOnLoad { get; set; }

    public int HttpPort { get; set; } = 38473;
}
