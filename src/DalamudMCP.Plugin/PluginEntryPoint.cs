using System.Runtime.Versioning;
using Dalamud.Plugin;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Hosting;
using DalamudMCP.Plugin.Ui;

namespace DalamudMCP.Plugin;

[SupportedOSPlatform("windows")]
public sealed class PluginEntryPoint : IDalamudPlugin, IAsyncDisposable
{
    private readonly string name = "DalamudMCP";
    private readonly IDalamudPluginInterface? pluginInterface;
    private readonly PluginUiConfigurationStore? configurationStore;
    private readonly PluginHostController? hostController;
    private readonly PluginConfigWindow? configWindow;

    public PluginEntryPoint(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.pluginInterface = pluginInterface;
        CompositionRoot = PluginCompositionRoot.CreateFromDalamud(pluginInterface);
        CompositionRoot.StartAsync().GetAwaiter().GetResult();
        configurationStore = PluginUiConfigurationStore.Load(pluginInterface);
        hostController = new PluginHostController(
            new PluginHostPathResolver(
                pluginInterface.AssemblyLocation.FullName,
                CompositionRoot.Options.PipeName));
        configWindow = new PluginConfigWindow(CompositionRoot, configurationStore, hostController);
        HookUi(pluginInterface);

        if (configurationStore.Current.AutoLaunchHttpServerOnLoad)
        {
            hostController.TryStartHttpServer(configurationStore.Current.HttpPort);
        }
    }

    public PluginCompositionRoot? CompositionRoot { get; private set; }

    public string Name => name;

    public async Task InitializeAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        CompositionRoot = PluginCompositionRoot.CreateDefault(workingDirectory);
        await CompositionRoot.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (pluginInterface is not null)
        {
            UnhookUi(pluginInterface);
        }

        hostController?.Dispose();

        if (CompositionRoot is null)
        {
            return;
        }

        await CompositionRoot.StopAsync(cancellationToken).ConfigureAwait(false);
        await CompositionRoot.DisposeAsync().ConfigureAwait(false);
        CompositionRoot = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private void HookUi(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.UiBuilder.Draw += DrawConfigUi;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
    }

    private void UnhookUi(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.UiBuilder.Draw -= DrawConfigUi;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
    }

    private void DrawConfigUi()
    {
        configWindow?.Draw();
    }

    private void OpenConfigUi()
    {
        configWindow?.Open();
    }
}
