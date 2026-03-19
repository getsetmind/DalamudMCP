using Dalamud.Plugin;

namespace DalamudMCP.Plugin.Configuration;

public sealed class PluginUiConfigurationStore
{
    private readonly IDalamudPluginInterface pluginInterface;

    private PluginUiConfigurationStore(
        IDalamudPluginInterface pluginInterface,
        PluginUiConfiguration current)
    {
        this.pluginInterface = pluginInterface;
        Current = current;
    }

    public PluginUiConfiguration Current { get; private set; }

    public static PluginUiConfigurationStore Load(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);

        var configuration = pluginInterface.GetPluginConfig() as PluginUiConfiguration ?? new PluginUiConfiguration();
        return new PluginUiConfigurationStore(pluginInterface, configuration);
    }

    public void Update(Action<PluginUiConfiguration> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        update(Current);
        Save();
    }

    public void Save()
    {
        pluginInterface.SavePluginConfig(Current);
    }
}
