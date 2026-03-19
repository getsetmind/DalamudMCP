using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.UseCases.Action;
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
        var nearbyInteractablesReader = new NullNearbyInteractablesReader();
        var targetObjectController = new NullTargetObjectController();
        var interactWithTargetController = new NullInteractWithTargetController();
        var entityMovementController = new NullEntityMovementController();
        var aetheryteTeleportController = new NullAetheryteTeleportController();
        var addonCallbackController = new NullAddonCallbackController();
        return CreateCore(
            options,
            playerContextReader,
            dutyContextReader,
            inventorySummaryReader,
            addonCatalogReader,
            addonTreeReader,
            stringTableReader,
            nearbyInteractablesReader,
            targetObjectController,
            interactWithTargetController,
            entityMovementController,
            aetheryteTeleportController,
            addonCallbackController);
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
        var inventorySummaryReader = CreateInventorySummaryReader(pluginInterface, pluginLog);
        var addonCatalogReader = CreateAddonCatalogReader(pluginInterface, pluginLog);
        var addonTreeReader = CreateAddonTreeReader(pluginInterface, pluginLog);
        var stringTableReader = CreateStringTableReader(pluginInterface, pluginLog);
        var nearbyInteractablesReader = CreateNearbyInteractablesReader(pluginInterface, pluginLog);
        var targetObjectController = CreateTargetObjectController(pluginInterface, pluginLog);
        var interactWithTargetController = CreateInteractWithTargetController(pluginInterface, pluginLog);
        var entityMovementController = CreateEntityMovementController(pluginInterface, pluginLog);
        var aetheryteTeleportController = CreateAetheryteTeleportController(pluginInterface, pluginLog);
        var addonCallbackController = CreateAddonCallbackController(pluginInterface, pluginLog);
        return CreateCore(
            options,
            playerContextReader,
            dutyContextReader,
            inventorySummaryReader,
            addonCatalogReader,
            addonTreeReader,
            stringTableReader,
            nearbyInteractablesReader,
            targetObjectController,
            interactWithTargetController,
            entityMovementController,
            aetheryteTeleportController,
            addonCallbackController,
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
        INearbyInteractablesReader nearbyInteractablesReader,
        ITargetObjectController targetObjectController,
        IInteractWithTargetController interactWithTargetController,
        IEntityMovementController entityMovementController,
        IAetheryteTeleportController aetheryteTeleportController,
        IAddonCallbackController addonCallbackController,
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
        var getNearbyInteractablesUseCase = new GetNearbyInteractablesUseCase(
            nearbyInteractablesReader,
            settingsRepository,
            capabilityRegistry,
            freshnessPolicy);
        var targetObjectUseCase = new TargetObjectUseCase(
            targetObjectController,
            settingsRepository,
            capabilityRegistry);
        var interactWithTargetUseCase = new InteractWithTargetUseCase(
            interactWithTargetController,
            settingsRepository,
            capabilityRegistry);
        var moveToEntityUseCase = new MoveToEntityUseCase(
            entityMovementController,
            settingsRepository,
            capabilityRegistry);
        var teleportToAetheryteUseCase = new TeleportToAetheryteUseCase(
            aetheryteTeleportController,
            settingsRepository,
            capabilityRegistry);
        var sendAddonCallbackIntUseCase = new SendAddonCallbackIntUseCase(
            addonCallbackController,
            settingsRepository,
            capabilityRegistry);
        var sendAddonCallbackValuesUseCase = new SendAddonCallbackValuesUseCase(
            addonCallbackController,
            settingsRepository,
            capabilityRegistry);
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
            getNearbyInteractablesUseCase,
            targetObjectUseCase,
            interactWithTargetUseCase,
            moveToEntityUseCase,
            teleportToAetheryteUseCase,
            sendAddonCallbackIntUseCase,
            sendAddonCallbackValuesUseCase,
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
    private static IInventorySummaryReader CreateInventorySummaryReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IInventorySummaryReader? reader = pluginInterface.Create<DalamudInventorySummaryReader>();
            return reader ?? new NullInventorySummaryReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullInventorySummaryReader because DalamudInventorySummaryReader could not be created.");
            return new NullInventorySummaryReader();
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
    private static INearbyInteractablesReader CreateNearbyInteractablesReader(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            INearbyInteractablesReader? reader = pluginInterface.Create<DalamudNearbyInteractablesReader>();
            return reader ?? new NullNearbyInteractablesReader();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullNearbyInteractablesReader because DalamudNearbyInteractablesReader could not be created.");
            return new NullNearbyInteractablesReader();
        }
    }

    [SupportedOSPlatform("windows")]
    private static ITargetObjectController CreateTargetObjectController(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            ITargetObjectController? controller = pluginInterface.Create<DalamudTargetObjectController>();
            return controller ?? new NullTargetObjectController();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullTargetObjectController because DalamudTargetObjectController could not be created.");
            return new NullTargetObjectController();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IInteractWithTargetController CreateInteractWithTargetController(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IInteractWithTargetController? controller = pluginInterface.Create<DalamudInteractWithTargetController>();
            return controller ?? new NullInteractWithTargetController();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullInteractWithTargetController because DalamudInteractWithTargetController could not be created.");
            return new NullInteractWithTargetController();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEntityMovementController CreateEntityMovementController(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IEntityMovementController? controller = pluginInterface.Create<DalamudVnavmeshMovementController>();
            return controller ?? new NullEntityMovementController();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullEntityMovementController because DalamudVnavmeshMovementController could not be created.");
            return new NullEntityMovementController();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IAetheryteTeleportController CreateAetheryteTeleportController(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IAetheryteTeleportController? controller = pluginInterface.Create<DalamudAetheryteTeleportController>();
            return controller ?? new NullAetheryteTeleportController();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullAetheryteTeleportController because DalamudAetheryteTeleportController could not be created.");
            return new NullAetheryteTeleportController();
        }
    }

    [SupportedOSPlatform("windows")]
    private static IAddonCallbackController CreateAddonCallbackController(IDalamudPluginInterface pluginInterface, IPluginLog? pluginLog)
    {
        try
        {
            IAddonCallbackController? controller = pluginInterface.Create<DalamudAddonCallbackController>();
            return controller ?? new NullAddonCallbackController();
        }
        catch (Exception exception)
        {
            pluginLog?.Warning(exception, "Falling back to NullAddonCallbackController because DalamudAddonCallbackController could not be created.");
            return new NullAddonCallbackController();
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
