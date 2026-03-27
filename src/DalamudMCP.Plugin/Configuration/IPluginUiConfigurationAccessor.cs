namespace DalamudMCP.Plugin.Configuration;

public interface IPluginUiConfigurationAccessor
{
    public PluginUiConfiguration Current { get; }
}
