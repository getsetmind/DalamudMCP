using Dalamud.Plugin;

namespace DalamudMCP.Plugin.Configuration;

public sealed class PluginUiConfigurationStore : IPluginUiConfigurationAccessor
{
    private readonly IDalamudPluginInterface? pluginInterface;

    private PluginUiConfigurationStore(IDalamudPluginInterface? pluginInterface, PluginUiConfiguration current)
    {
        this.pluginInterface = pluginInterface;
        Current = current;
    }

    public PluginUiConfiguration Current { get; private set; }

    public static PluginUiConfigurationStore Load(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);

        PluginUiConfiguration configuration =
            pluginInterface.GetPluginConfig() as PluginUiConfiguration ?? new PluginUiConfiguration();
        if (configuration.Version < 5 || string.IsNullOrWhiteSpace(configuration.HttpBearerToken))
        {
            configuration.Version = 5;
            configuration.HttpBearerToken = PluginUiConfiguration.CreateBearerToken();
            pluginInterface.SavePluginConfig(configuration);
        }

        return new PluginUiConfigurationStore(pluginInterface, configuration);
    }

    internal static PluginUiConfigurationStore CreateForTests(PluginUiConfiguration? current = null)
    {
        return new PluginUiConfigurationStore(pluginInterface: null, current ?? new PluginUiConfiguration());
    }

    public void Update(Action<PluginUiConfiguration> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        update(Current);
        pluginInterface?.SavePluginConfig(Current);
    }
}
