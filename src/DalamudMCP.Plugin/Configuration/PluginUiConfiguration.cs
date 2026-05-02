using Dalamud.Configuration;

namespace DalamudMCP.Plugin.Configuration;

public sealed class PluginUiConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    public bool AutoStartHttpServerOnLoad { get; set; }

    public bool EnableActionOperations { get; set; }

    public bool EnableUnsafeOperations { get; set; }

    public string SelectedLanguage { get; set; } = "en";
}
