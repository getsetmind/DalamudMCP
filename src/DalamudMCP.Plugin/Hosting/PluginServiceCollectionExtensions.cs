using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Plugin.Relay;
using DalamudMCP.Plugin.Ui.Localization;
using DalamudMCP.Protocol;
using Manifold;
using Manifold.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Plugin.Hosting;

[SupportedOSPlatform("windows")]
public static class PluginServiceCollectionExtensions
{
    public static ServiceProvider BuildDalamudServiceProvider(
        IDalamudPluginInterface pluginInterface,
        Configuration.PluginUiConfigurationStore configurationStore,
        PluginRuntimeOptions options,
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
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(options);
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

        ServiceCollection services = new();
        services.AddSingleton(options);
        services.AddSingleton(pluginInterface);
        services.AddSingleton(configurationStore);
        services.AddSingleton<Configuration.IPluginUiConfigurationAccessor>(configurationStore);
        services.AddSingleton<IUiLocalization>(_ => new JsonLocalization(configurationStore.Current.SelectedLanguage));
        services.AddSingleton(framework);
        services.AddSingleton(clientState);
        services.AddSingleton(condition);
        services.AddSingleton(objectTable);
        services.AddSingleton(playerState);
        services.AddSingleton(gameInventory);
        services.AddSingleton(fateTable);
        services.AddSingleton(dataManager);
        services.AddSingleton(gameGui);
        services.AddSingleton(chatGui);
        services.AddSingleton(targetManager);
        services.AddSingleton(commandManager);
        services.AddSingleton<Services.ChatLogBufferService>();
        services.AddSingleton<IPluginDataRelayService, PluginDataRelayService>();
        services.AddGeneratedPluginOperations();
        services.AddSingleton<IOperationInvoker, GeneratedOperationInvoker>();
        services.AddSingleton<IReadOnlyList<OperationDescriptor>>(static _ => GeneratedOperationRegistry.Operations);
        services.AddSingleton<OperationProtocolDispatcher>();
        services.AddSingleton(static provider =>
            new NamedPipeProtocolServer(
                provider.GetRequiredService<PluginRuntimeOptions>().PipeName,
                provider.GetRequiredService<OperationProtocolDispatcher>().DispatchAsync));

        return services.BuildServiceProvider();
    }
}
