using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Plugin.Ui;
using DalamudMCP.Plugin.Ui.Localization;
using DalamudMCP.Protocol;
using Manifold;

namespace DalamudMCP.Plugin;

[SupportedOSPlatform("windows")]
public sealed class PluginEntryPoint : IDalamudPlugin
{
    private readonly PluginCompositionRoot compositionRoot;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PluginConfigWindow configWindow;
    private readonly PluginUiConfigurationStore configurationStore;
    private readonly Hosting.PluginMcpServerController mcpServerController;

    public PluginEntryPoint(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objectTable,
        IPlayerState playerState,
        IGameInventory gameInventory,
        IFateTable fateTable,
        IDataManager dataManager,
        IGameGui gameGui,
        IChatGui chatGui,
        ITargetManager targetManager,
        ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(playerState);
        ArgumentNullException.ThrowIfNull(gameInventory);
        ArgumentNullException.ThrowIfNull(fateTable);
        ArgumentNullException.ThrowIfNull(dataManager);
        ArgumentNullException.ThrowIfNull(gameGui);
        ArgumentNullException.ThrowIfNull(chatGui);
        ArgumentNullException.ThrowIfNull(targetManager);
        ArgumentNullException.ThrowIfNull(commandManager);

        this.pluginInterface = pluginInterface;
        configurationStore = PluginUiConfigurationStore.Load(pluginInterface);
        compositionRoot = PluginCompositionRoot.CreateFromDalamud(
            pluginInterface,
            configurationStore,
            framework,
            clientState,
            condition,
            objectTable,
            playerState,
            gameInventory,
            fateTable,
            dataManager,
            gameGui,
            chatGui,
            targetManager,
            commandManager);
        compositionRoot.StartAsync().GetAwaiter().GetResult();
        ProtocolClientDiscovery.Write(
            new ProtocolClientDiscoveryRecord(
                compositionRoot.Options.PipeName,
                Environment.ProcessId,
                DateTimeOffset.UtcNow),
            compositionRoot.Options.WorkingDirectory);
        string pluginAssemblyPath = pluginInterface.AssemblyLocation.FullName;
        IReadOnlyList<OperationDescriptor> operations = compositionRoot.GetRequiredService<IReadOnlyList<OperationDescriptor>>();
        mcpServerController = new Hosting.PluginMcpServerController(
            new Hosting.PluginCliPathResolver(
                pluginAssemblyPath,
                compositionRoot.Options.PipeName),
            () => Hosting.PluginOperationExposurePolicy.GetExpectedMcpToolNames(
                operations,
                configurationStore.Current.EnableActionOperations,
                configurationStore.Current.EnableUnsafeOperations));
        configWindow = new PluginConfigWindow(
            compositionRoot.Options,
            compositionRoot.ProtocolServer,
            configurationStore,
            mcpServerController,
            operations,
            compositionRoot.GetServices<IPluginReaderStatus>(),
            compositionRoot.GetRequiredService<IUiLocalization>());
        if (configurationStore.Current.AutoStartHttpServerOnLoad)
            _ = mcpServerController.Start();
        HookUi(pluginInterface);
    }

    public string Name { get; } = "DalamudMCP";

    public void Dispose()
    {
        UnhookUi(pluginInterface);
        mcpServerController.Dispose();
        ProtocolClientDiscovery.DeleteIfMatches(compositionRoot.Options.PipeName, compositionRoot.Options.WorkingDirectory);
        compositionRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
        configWindow.Draw();
    }

    private void OpenConfigUi()
    {
        configWindow.Open();
    }
}
