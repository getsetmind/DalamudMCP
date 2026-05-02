using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Plugin;

[SupportedOSPlatform("windows")]
public sealed class PluginCompositionRoot : IAsyncDisposable
{
    private readonly ServiceProvider serviceProvider;

    private PluginCompositionRoot(
        ServiceProvider serviceProvider,
        PluginRuntimeOptions options,
        NamedPipeProtocolServer protocolServer)
    {
        this.serviceProvider = serviceProvider;
        Options = options;
        ProtocolServer = protocolServer;
    }

    public PluginRuntimeOptions Options { get; }

    public NamedPipeProtocolServer ProtocolServer { get; }

    public TService GetRequiredService<TService>()
        where TService : notnull
    {
        return serviceProvider.GetRequiredService<TService>();
    }

    public IReadOnlyList<TService> GetServices<TService>()
        where TService : notnull
    {
        return serviceProvider.GetServices<TService>().ToArray();
    }

    public static PluginCompositionRoot CreateFromDalamud(
        IDalamudPluginInterface pluginInterface,
        Configuration.PluginUiConfigurationStore configurationStore,
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
        ICommandManager commandManager,
        string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(configurationStore);
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

        PluginRuntimeOptions options = PluginRuntimeOptions.CreateDefault(pluginInterface.ConfigDirectory.FullName, pipeName);
        ServiceProvider serviceProvider = Hosting.PluginServiceCollectionExtensions.BuildDalamudServiceProvider(
            pluginInterface,
            configurationStore,
            options,
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

        return new PluginCompositionRoot(
            serviceProvider,
            options,
            serviceProvider.GetRequiredService<NamedPipeProtocolServer>());
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return ProtocolServer.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await ProtocolServer.StopAsync().ConfigureAwait(false);
        await serviceProvider.DisposeAsync().ConfigureAwait(false);
    }
}
