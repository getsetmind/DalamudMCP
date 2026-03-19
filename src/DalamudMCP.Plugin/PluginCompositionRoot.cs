using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Infrastructure.Audit;
using DalamudMCP.Infrastructure.Bridge;
using DalamudMCP.Infrastructure.Settings;
using DalamudMCP.Infrastructure.Time;
using DalamudMCP.Plugin.Readers;

namespace DalamudMCP.Plugin;

public sealed class PluginCompositionRoot : IAsyncDisposable
{
    private PluginCompositionRoot(
        PluginRuntimeOptions options,
        CapabilityRegistry capabilityRegistry,
        ISettingsRepository settingsRepository,
        IAuditLogWriter auditLogWriter,
        BridgeRequestDispatcher bridgeDispatcher,
        NamedPipeBridgeServer bridgeServer)
    {
        Options = options;
        CapabilityRegistry = capabilityRegistry;
        SettingsRepository = settingsRepository;
        AuditLogWriter = auditLogWriter;
        BridgeDispatcher = bridgeDispatcher;
        BridgeServer = bridgeServer;
    }

    public PluginRuntimeOptions Options { get; }

    public CapabilityRegistry CapabilityRegistry { get; }

    public ISettingsRepository SettingsRepository { get; }

    public IAuditLogWriter AuditLogWriter { get; }

    public BridgeRequestDispatcher BridgeDispatcher { get; }

    public NamedPipeBridgeServer BridgeServer { get; }

    public static PluginCompositionRoot CreateDefault(string workingDirectory, string? pipeName = null)
    {
        var options = PluginRuntimeOptions.CreateDefault(workingDirectory, pipeName);
        var playerContextReader = new NullPlayerContextReader();
        var dutyContextReader = new NullDutyContextReader();
        var inventorySummaryReader = new NullInventorySummaryReader();
        var addonCatalogReader = new EmptyAddonCatalogReader();
        var addonTreeReader = new NullAddonTreeReader();
        var stringTableReader = new NullStringTableReader();
        return CreateCore(
            options,
            playerContextReader,
            dutyContextReader,
            inventorySummaryReader,
            addonCatalogReader,
            addonTreeReader,
            stringTableReader);
    }

    [SupportedOSPlatform("windows")]
    public static PluginCompositionRoot CreateFromDalamud(
        IDalamudPluginInterface pluginInterface,
        string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);

        var options = PluginRuntimeOptions.CreateDefault(pluginInterface.ConfigDirectory.FullName, pipeName);
        var pluginLog = TryCreatePluginLog(pluginInterface);
        var framework = TryCreateFramework(pluginInterface);
        var playerContextReader = CreatePlayerContextReader(pluginInterface, pluginLog);
        var dutyContextReader = CreateDutyContextReader(pluginInterface, pluginLog);
        var inventorySummaryReader = new NullInventorySummaryReader();
        var addonCatalogReader = CreateAddonCatalogReader(pluginInterface, pluginLog);
        var addonTreeReader = CreateAddonTreeReader(pluginInterface, pluginLog);
        var stringTableReader = CreateStringTableReader(pluginInterface, pluginLog);
        return CreateCore(
            options,
            playerContextReader,
            dutyContextReader,
            inventorySummaryReader,
            addonCatalogReader,
            addonTreeReader,
            stringTableReader,
            framework);
    }

    private static PluginCompositionRoot CreateCore(
        PluginRuntimeOptions options,
        IPlayerContextReader playerContextReader,
        IDutyContextReader dutyContextReader,
        IInventorySummaryReader inventorySummaryReader,
        IAddonCatalogReader addonCatalogReader,
        IAddonTreeReader addonTreeReader,
        IStringTableReader stringTableReader,
        IFramework? framework = null)
    {
        var capabilityRegistry = KnownCapabilityRegistry.CreateDefault();
        var settingsRepository = new JsonSettingsRepository(options.SettingsFilePath);
        var auditLogWriter = new FileAuditLogWriter(options.AuditLogFilePath);
        var clock = new SystemClock();
        var freshnessPolicy = new SnapshotFreshnessPolicy(clock);
        NamedPipeBridgeServer? bridgeServer = null;
        var diagnostics = new IPluginReaderDiagnostics[]
        {
            (IPluginReaderDiagnostics)playerContextReader,
            (IPluginReaderDiagnostics)dutyContextReader,
            (IPluginReaderDiagnostics)inventorySummaryReader,
            (IPluginReaderDiagnostics)addonCatalogReader,
            (IPluginReaderDiagnostics)addonTreeReader,
            (IPluginReaderDiagnostics)stringTableReader,
        };
        var sessionStateReader = new PluginSessionStateReader(
            clock,
            options.PipeName,
            () => bridgeServer?.IsRunning is true,
            diagnostics,
            framework);
        var getSessionStatusUseCase = new GetSessionStatusUseCase(
            sessionStateReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);

        var getPlayerContextUseCase = new GetPlayerContextUseCase(
            playerContextReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var getDutyContextUseCase = new GetDutyContextUseCase(
            dutyContextReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var getInventorySummaryUseCase = new GetInventorySummaryUseCase(
            inventorySummaryReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var getAddonListUseCase = new GetAddonListUseCase(
            addonCatalogReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var getAddonTreeUseCase = new GetAddonTreeUseCase(
            addonTreeReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var getAddonStringsUseCase = new GetAddonStringsUseCase(
            stringTableReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var getCurrentSettingsUseCase = new GetCurrentSettingsUseCase(settingsRepository);
        var recordAuditEventUseCase = new RecordAuditEventUseCase(auditLogWriter);

        var dispatcher = new BridgeRequestDispatcher(
            getSessionStatusUseCase,
            getPlayerContextUseCase,
            getDutyContextUseCase,
            getInventorySummaryUseCase,
            getAddonListUseCase,
            getAddonTreeUseCase,
            getAddonStringsUseCase,
            getCurrentSettingsUseCase,
            recordAuditEventUseCase);
        bridgeServer = new NamedPipeBridgeServer(options.PipeName, dispatcher.DispatchAsync);

        return new PluginCompositionRoot(
            options,
            capabilityRegistry,
            settingsRepository,
            auditLogWriter,
            dispatcher,
            bridgeServer);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        BridgeServer.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        BridgeServer.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await BridgeServer.DisposeAsync().ConfigureAwait(false);
    }

    [SupportedOSPlatform("windows")]
    private static IPlayerContextReader CreatePlayerContextReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IPlayerContextReader? reader = pluginInterface.Create<DalamudPlayerContextReader>();
            return reader ?? new NullPlayerContextReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullPlayerContextReader because DalamudPlayerContextReader could not be created.");
            return new NullPlayerContextReader();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IDutyContextReader CreateDutyContextReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IDutyContextReader? reader = pluginInterface.Create<DalamudDutyContextReader>();
            return reader ?? new NullDutyContextReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullDutyContextReader because DalamudDutyContextReader could not be created.");
            return new NullDutyContextReader();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IAddonCatalogReader CreateAddonCatalogReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IAddonCatalogReader? reader = pluginInterface.Create<DalamudAddonCatalogReader>();
            return reader ?? new EmptyAddonCatalogReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to EmptyAddonCatalogReader because DalamudAddonCatalogReader could not be created.");
            return new EmptyAddonCatalogReader();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IAddonTreeReader CreateAddonTreeReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IAddonTreeReader? reader = pluginInterface.Create<DalamudAddonTreeReader>();
            return reader ?? new NullAddonTreeReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullAddonTreeReader because DalamudAddonTreeReader could not be created.");
            return new NullAddonTreeReader();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IStringTableReader CreateStringTableReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IStringTableReader? reader = pluginInterface.Create<DalamudStringTableReader>();
            return reader ?? new NullStringTableReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullStringTableReader because DalamudStringTableReader could not be created.");
            return new NullStringTableReader();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IPluginLog? TryCreatePluginLog(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            return pluginInterface.Create<IPluginLog>();
        }
        catch
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IFramework? TryCreateFramework(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            return pluginInterface.Create<IFramework>();
        }
        catch
        {
            return null;
        }
    }
}
