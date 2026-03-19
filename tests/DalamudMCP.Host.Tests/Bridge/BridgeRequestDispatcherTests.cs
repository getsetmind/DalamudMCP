using DalamudMCP.Application.Abstractions;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.UseCases.Action;
using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Contracts.Bridge.Responses;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Audit;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Session;
using DalamudMCP.Domain.Snapshots;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Host.Tests.Bridge;

public sealed class BridgeRequestDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ReturnsMappedPlayerContextPayload()
    {
        var dispatcher = CreateDispatcher(
            playerContext: new PlayerContextSnapshot(
                new DateTimeOffset(2026, 3, 20, 2, 0, 0, TimeSpan.Zero),
                "Alice",
                "Tonberry",
                "Tonberry",
                24,
                "White Mage",
                100,
                1,
                "Limsa",
                2,
                "Lower Decks",
                new PositionSnapshot(1, 2, 3, "coarse"),
                false,
                false,
                false,
                false,
                false,
                false,
                "city",
                "idle",
                "Lv100 White Mage"));

        var response = await dispatcher.DispatchAsync(
            new BridgeRequestEnvelope(ContractVersion.Current, BridgeRequestTypes.GetPlayerContext, "req-1", new EmptyRequest()),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(BridgeResponseTypes.Query, response.ResponseType);
        var payload = BridgeJson.DeserializePayload<QueryResponse<PlayerContextContract>>(response.Payload);
        Assert.NotNull(payload);
        Assert.True(payload.Available);
        Assert.Equal("Alice", payload.Data?.CharacterName);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsMappedSessionStatusPayload()
    {
        var dispatcher = CreateDispatcher(
            sessionState: new SessionState(
                new DateTimeOffset(2026, 3, 20, 2, 0, 0, TimeSpan.Zero),
                "DalamudMCP.TestPipe",
                true,
                [new SessionComponentState("player_context", false, "not_attached")],
                "0/1 readers ready; bridge server running."));

        var response = await dispatcher.DispatchAsync(
            new BridgeRequestEnvelope(ContractVersion.Current, BridgeRequestTypes.GetSessionStatus, "req-session", new EmptyRequest()),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(BridgeResponseTypes.Query, response.ResponseType);
        var payload = BridgeJson.DeserializePayload<QueryResponse<SessionStateContract>>(response.Payload);
        Assert.NotNull(payload);
        Assert.True(payload.Available);
        Assert.Equal("DalamudMCP.TestPipe", payload.Data?.PipeName);
    }

    [Fact]
    public async Task DispatchAsync_UsesAddonPayloadForTreeQueries()
    {
        var dispatcher = CreateDispatcher(
            playerContext: null,
            addonTree: new AddonTreeSnapshot(
                "Inventory",
                new DateTimeOffset(2026, 3, 20, 2, 0, 0, TimeSpan.Zero),
                [new NodeSnapshot(1, "TextNode", true, 10, 20, 30, 40, "Hello", [])]));

        var response = await dispatcher.DispatchAsync(
            new BridgeRequestEnvelope(ContractVersion.Current, BridgeRequestTypes.GetAddonTree, "req-2", new AddonRequest("Inventory")),
            CancellationToken.None);

        var payload = BridgeJson.DeserializePayload<QueryResponse<AddonTreeContract>>(response.Payload);
        Assert.NotNull(payload);
        Assert.True(payload.Available);
        Assert.Equal("Inventory", payload.Data?.AddonName);
        Assert.Single(payload.Data!.Roots);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsCapabilityStatePayload()
    {
        var policy = new ExposurePolicy(
            enabledTools: ["get_player_context"],
            enabledResources: ["ffxiv://player/context"],
            enabledAddons: ["Inventory"]);
        var dispatcher = CreateDispatcher(policy: policy);

        var response = await dispatcher.DispatchAsync(
            new BridgeRequestEnvelope(ContractVersion.Current, BridgeRequestTypes.GetCapabilityState, "req-3", new EmptyRequest()),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(BridgeResponseTypes.CapabilityState, response.ResponseType);
        var payload = BridgeJson.DeserializePayload<CapabilityStateResponse>(response.Payload);
        Assert.NotNull(payload);
        Assert.Contains("get_player_context", payload.EnabledTools);
        Assert.Contains("Inventory", payload.EnabledAddons);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("broken")]
    public async Task DispatchAsync_RejectsIncompatibleContractVersion(string version)
    {
        var dispatcher = CreateDispatcher();

        var response = await dispatcher.DispatchAsync(
            new BridgeRequestEnvelope(version, BridgeRequestTypes.GetPlayerContext, "req-version", new EmptyRequest()),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("invalid_contract_version", response.ErrorCode);
    }

    [Fact]
    public async Task HandleJsonAsync_ReturnsInvalidRequestErrorForMalformedJson()
    {
        var host = new McpServerHost(CreateDispatcher());

        var json = await host.HandleJsonAsync("{", CancellationToken.None);
        var response = BridgeJson.Deserialize<BridgeResponseEnvelope>(json);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("invalid_request", response.ErrorCode);
    }

    [Fact]
    public async Task DispatchAsync_RecordAuditEvent_WritesAuditEntry()
    {
        var auditLogWriter = new InMemoryAuditLogWriter();
        var dispatcher = CreateDispatcher(auditLogWriter: auditLogWriter);

        var response = await dispatcher.DispatchAsync(
            new BridgeRequestEnvelope(ContractVersion.Current, BridgeRequestTypes.RecordAuditEvent, "req-audit", new AuditEventRequest("tool.request_denied", "tool=blocked_tool")),
            CancellationToken.None);

        Assert.True(response.Success);
        var auditEvent = Assert.Single(auditLogWriter.Events);
        Assert.Equal("tool.request_denied", auditEvent.EventType);
        Assert.Equal("tool=blocked_tool", auditEvent.Summary);
    }

    private static BridgeRequestDispatcher CreateDispatcher(
        SessionState? sessionState = null,
        PlayerContextSnapshot? playerContext = null,
        DutyContextSnapshot? dutyContext = null,
        InventorySummarySnapshot? inventorySummary = null,
        IReadOnlyList<AddonSummary>? addonList = null,
        AddonTreeSnapshot? addonTree = null,
        StringTableSnapshot? addonStrings = null,
        NearbyInteractablesSnapshot? nearbyInteractables = null,
        ExposurePolicy? policy = null,
        InMemoryAuditLogWriter? auditLogWriter = null,
        TargetObjectResult? targetObjectResult = null,
        InteractWithTargetResult? interactWithTargetResult = null,
        MoveToEntityResult? moveToEntityResult = null,
        TeleportToAetheryteResult? teleportToAetheryteResult = null)
    {
        var settingsRepository = new StubSettingsRepository(
            policy ?? new ExposurePolicy(
                enabledTools: ["get_session_status", "get_player_context", "get_duty_context", "get_inventory_summary", "get_addon_list", "get_addon_tree", "get_addon_strings", "get_nearby_interactables", "target_object", "interact_with_target", "move_to_entity", "teleport_to_aetheryte", "send_addon_callback_int", "send_addon_callback_values"],
                enabledResources: ["ffxiv://session/status"],
                enabledAddons: ["Inventory", "TelepotTown"],
                observationProfileEnabled: true,
                actionProfileEnabled: true));
        var capabilityRegistry = CreateRegistry();
        var freshnessPolicy = new SnapshotFreshnessPolicy(
            new StubClock(new DateTimeOffset(2026, 3, 20, 2, 0, 1, TimeSpan.Zero)));

        return new BridgeRequestDispatcher(
            new GetSessionStatusUseCase(new StubSessionStateReader(sessionState), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetPlayerContextUseCase(new StubPlayerContextReader(playerContext), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetDutyContextUseCase(new StubDutyContextReader(dutyContext), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetInventorySummaryUseCase(new StubInventorySummaryReader(inventorySummary), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetAddonListUseCase(new StubAddonCatalogReader(addonList ?? []), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetAddonTreeUseCase(new StubAddonTreeReader(addonTree), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetAddonStringsUseCase(new StubStringTableReader(addonStrings), settingsRepository, capabilityRegistry, freshnessPolicy),
            new GetNearbyInteractablesUseCase(new StubNearbyInteractablesReader(nearbyInteractables), settingsRepository, capabilityRegistry, freshnessPolicy),
            new TargetObjectUseCase(new StubTargetObjectController(targetObjectResult), settingsRepository, capabilityRegistry),
            new InteractWithTargetUseCase(new StubInteractWithTargetController(interactWithTargetResult), settingsRepository, capabilityRegistry),
            new MoveToEntityUseCase(new StubEntityMovementController(moveToEntityResult), settingsRepository, capabilityRegistry),
            new TeleportToAetheryteUseCase(new StubAetheryteTeleportController(teleportToAetheryteResult), settingsRepository, capabilityRegistry),
            new SendAddonCallbackIntUseCase(new StubAddonCallbackController(), settingsRepository, capabilityRegistry),
            new SendAddonCallbackValuesUseCase(new StubAddonCallbackController(), settingsRepository, capabilityRegistry),
            new GetCurrentSettingsUseCase(settingsRepository),
            new RecordAuditEventUseCase(auditLogWriter ?? new InMemoryAuditLogWriter()));
    }

    private static CapabilityRegistry CreateRegistry() =>
        new(
            [
                CreateCapability("session.status", "Session Status", CapabilityCategory.System),
                CreateCapability("player.context", "Player Context", CapabilityCategory.Player),
                CreateCapability("duty.context", "Duty Context", CapabilityCategory.Duty),
                CreateCapability("inventory.summary", "Inventory Summary", CapabilityCategory.Inventory),
                CreateCapability("ui.addonCatalog", "Addon Catalog", CapabilityCategory.Ui),
                CreateCapability("ui.addonTree", "Addon Tree", CapabilityCategory.Ui),
                CreateCapability("ui.stringTable", "String Table", CapabilityCategory.Ui),
                CreateCapability("world.nearbyInteractables", "Nearby Interactables", CapabilityCategory.Target),
                new CapabilityDefinition(
                    new CapabilityId("world.targetObject"),
                    "Target Object",
                    "Target Object capability",
                    CapabilityCategory.Action,
                    SensitivityLevel.High,
                    ProfileType.Action,
                    defaultEnabled: false,
                    requiresConsent: true,
                    denied: false,
                    supportsTool: true,
                    supportsResource: false,
                    version: "1.0.0",
                    "test"),
                new CapabilityDefinition(
                    new CapabilityId("world.interactWithTarget"),
                    "Interact With Target",
                    "Interact With Target capability",
                    CapabilityCategory.Action,
                    SensitivityLevel.High,
                    ProfileType.Action,
                    defaultEnabled: false,
                    requiresConsent: true,
                    denied: false,
                    supportsTool: true,
                    supportsResource: false,
                    version: "1.0.0",
                    "test"),
                new CapabilityDefinition(
                    new CapabilityId("world.moveToEntity"),
                    "Move To Entity",
                    "Move To Entity capability",
                    CapabilityCategory.Action,
                    SensitivityLevel.High,
                    ProfileType.Action,
                    defaultEnabled: false,
                    requiresConsent: true,
                    denied: false,
                    supportsTool: true,
                    supportsResource: false,
                    version: "1.0.0",
                    "test"),
                new CapabilityDefinition(
                    new CapabilityId("world.teleportToAetheryte"),
                    "Teleport To Aetheryte",
                    "Teleport To Aetheryte capability",
                    CapabilityCategory.Action,
                    SensitivityLevel.High,
                    ProfileType.Action,
                    defaultEnabled: false,
                    requiresConsent: true,
                    denied: false,
                    supportsTool: true,
                    supportsResource: false,
                    version: "1.0.0",
                    "test"),
                new CapabilityDefinition(
                    new CapabilityId("ui.addonCallback"),
                    "Addon Callback",
                    "Addon Callback capability",
                    CapabilityCategory.Action,
                    SensitivityLevel.High,
                    ProfileType.Action,
                    defaultEnabled: false,
                    requiresConsent: true,
                    denied: false,
                    supportsTool: true,
                    supportsResource: false,
                    version: "1.0.0",
                    "test"),
            ],
            [
                CreateToolBinding("session.status", "get_session_status"),
                CreateToolBinding("player.context", "get_player_context"),
                CreateToolBinding("duty.context", "get_duty_context"),
                CreateToolBinding("inventory.summary", "get_inventory_summary"),
                CreateToolBinding("ui.addonCatalog", "get_addon_list"),
                CreateToolBinding("ui.addonTree", "get_addon_tree"),
                CreateToolBinding("ui.stringTable", "get_addon_strings"),
                CreateToolBinding("world.nearbyInteractables", "get_nearby_interactables"),
                new ToolBinding(new CapabilityId("world.targetObject"), "target_object", "target_object.input", "target_object.output", "TargetObjectToolHandler", Experimental: true),
                new ToolBinding(new CapabilityId("world.interactWithTarget"), "interact_with_target", "interactWithTarget.input", "interactWithTarget.output", "InteractWithTargetToolHandler", Experimental: true),
                new ToolBinding(new CapabilityId("world.moveToEntity"), "move_to_entity", "moveToEntity.input", "moveToEntity.output", "MoveToEntityToolHandler", Experimental: true),
                new ToolBinding(new CapabilityId("world.teleportToAetheryte"), "teleport_to_aetheryte", "teleportToAetheryte.input", "teleportToAetheryte.output", "TeleportToAetheryteToolHandler", Experimental: true),
                new ToolBinding(new CapabilityId("ui.addonCallback"), "send_addon_callback_int", "addonCallbackInt.input", "addonCallbackInt.output", "AddonCallbackIntToolHandler", Experimental: true),
                new ToolBinding(new CapabilityId("ui.addonCallback"), "send_addon_callback_values", "addonCallbackValues.input", "addonCallbackValues.output", "AddonCallbackValuesToolHandler", Experimental: true),
            ],
            [],
            [CreateAddonMetadata("Inventory"), CreateAddonMetadata("TelepotTown")]);

    private static CapabilityDefinition CreateCapability(string capabilityId, string displayName, CapabilityCategory category) =>
        new(
            new CapabilityId(capabilityId),
            displayName,
            $"{displayName} capability",
            category,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: false,
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0",
            "test");

    private static ToolBinding CreateToolBinding(string capabilityId, string toolName) =>
        new(
            new CapabilityId(capabilityId),
            toolName,
            $"{toolName}.input",
            $"{toolName}.output",
            $"{toolName}.handler",
            Experimental: false);

    private static AddonMetadata CreateAddonMetadata(string addonName) =>
        new(
            addonName,
            addonName,
            CapabilityCategory.Ui,
            SensitivityLevel.Low,
            DefaultEnabled: false,
            Denied: false,
            Recommended: true,
            Notes: "Test addon",
            IntrospectionModes: ["tree", "strings"],
            ProfileAvailability: [ProfileType.Observation]);

    private sealed class StubClock : IClock
    {
        public StubClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class StubSettingsRepository : ISettingsRepository
    {
        private ExposurePolicy policy;

        public StubSettingsRepository(ExposurePolicy policy)
        {
            this.policy = policy;
        }

        public Task<ExposurePolicy> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(policy);

        public Task SaveAsync(ExposurePolicy policy, CancellationToken cancellationToken)
        {
            this.policy = policy;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPlayerContextReader : IPlayerContextReader
    {
        private readonly PlayerContextSnapshot? snapshot;

        public StubPlayerContextReader(PlayerContextSnapshot? snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<PlayerContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class StubSessionStateReader : ISessionStateReader
    {
        private readonly SessionState snapshot;

        public StubSessionStateReader(SessionState? snapshot)
        {
            this.snapshot = snapshot ?? new SessionState(
                new DateTimeOffset(2026, 3, 20, 2, 0, 0, TimeSpan.Zero),
                "DalamudMCP.TestPipe",
                true,
                [new SessionComponentState("player_context", false, "not_attached")],
                "0/1 readers ready; bridge server running.");
        }

        public Task<SessionState> ReadCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class StubDutyContextReader : IDutyContextReader
    {
        private readonly DutyContextSnapshot? snapshot;

        public StubDutyContextReader(DutyContextSnapshot? snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<DutyContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class StubInventorySummaryReader : IInventorySummaryReader
    {
        private readonly InventorySummarySnapshot? snapshot;

        public StubInventorySummaryReader(InventorySummarySnapshot? snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<InventorySummarySnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class StubAddonCatalogReader : IAddonCatalogReader
    {
        private readonly IReadOnlyList<AddonSummary> snapshots;

        public StubAddonCatalogReader(IReadOnlyList<AddonSummary> snapshots)
        {
            this.snapshots = snapshots;
        }

        public Task<IReadOnlyList<AddonSummary>> ReadCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshots);
    }

    private sealed class StubAddonTreeReader : IAddonTreeReader
    {
        private readonly AddonTreeSnapshot? snapshot;

        public StubAddonTreeReader(AddonTreeSnapshot? snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<AddonTreeSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot?.AddonName.Equals(addonName, StringComparison.OrdinalIgnoreCase) is true ? snapshot : null);
    }

    private sealed class StubStringTableReader : IStringTableReader
    {
        private readonly StringTableSnapshot? snapshot;

        public StubStringTableReader(StringTableSnapshot? snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<StringTableSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot?.AddonName.Equals(addonName, StringComparison.OrdinalIgnoreCase) is true ? snapshot : null);
    }

    private sealed class StubNearbyInteractablesReader : INearbyInteractablesReader
    {
        private readonly NearbyInteractablesSnapshot? snapshot;

        public StubNearbyInteractablesReader(NearbyInteractablesSnapshot? snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<NearbyInteractablesSnapshot?> ReadCurrentAsync(double maxDistance, string? nameContains, bool includePlayers, CancellationToken cancellationToken)
        {
            _ = maxDistance;
            _ = nameContains;
            _ = includePlayers;
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StubTargetObjectController : Application.Abstractions.Actions.ITargetObjectController
    {
        private readonly TargetObjectResult result;

        public StubTargetObjectController(TargetObjectResult? result)
        {
            this.result = result ?? new TargetObjectResult(
                "0x0",
                Succeeded: false,
                Reason: "targeting_not_ready",
                TargetedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                SummaryText: "Targeting is not ready.");
        }

        public Task<TargetObjectResult> TargetAsync(string gameObjectId, CancellationToken cancellationToken)
        {
            _ = gameObjectId;
            return Task.FromResult(result);
        }
    }

    private sealed class StubInteractWithTargetController : Application.Abstractions.Actions.IInteractWithTargetController
    {
        private readonly InteractWithTargetResult result;

        public StubInteractWithTargetController(InteractWithTargetResult? result)
        {
            this.result = result ?? new InteractWithTargetResult(
                ExpectedGameObjectId: null,
                Succeeded: false,
                Reason: "interaction_not_ready",
                InteractedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Distance: null,
                SummaryText: "Interaction is not ready.");
        }

        public Task<InteractWithTargetResult> InteractAsync(string? expectedGameObjectId, bool checkLineOfSight, CancellationToken cancellationToken)
        {
            _ = expectedGameObjectId;
            _ = checkLineOfSight;
            return Task.FromResult(result);
        }
    }

    private sealed class StubEntityMovementController : Application.Abstractions.Actions.IEntityMovementController
    {
        private readonly MoveToEntityResult result;

        public StubEntityMovementController(MoveToEntityResult? result)
        {
            this.result = result ?? new MoveToEntityResult(
                RequestedGameObjectId: "0x0",
                Succeeded: false,
                Reason: "movement_not_ready",
                ResolvedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Destination: null,
                SummaryText: "Movement is not ready.");
        }

        public Task<MoveToEntityResult> MoveToEntityAsync(string gameObjectId, bool allowFlight, CancellationToken cancellationToken)
        {
            _ = gameObjectId;
            _ = allowFlight;
            return Task.FromResult(result);
        }
    }

    private sealed class StubAetheryteTeleportController : Application.Abstractions.Actions.IAetheryteTeleportController
    {
        private readonly TeleportToAetheryteResult result;

        public StubAetheryteTeleportController(TeleportToAetheryteResult? result)
        {
            this.result = result ?? new TeleportToAetheryteResult(
                RequestedQuery: "gold saucer",
                Succeeded: false,
                Reason: "teleport_not_ready",
                AetheryteId: null,
                AetheryteName: null,
                TerritoryName: null,
                SummaryText: "Teleport is not ready.");
        }

        public Task<TeleportToAetheryteResult> TeleportAsync(string query, CancellationToken cancellationToken)
        {
            _ = query;
            return Task.FromResult(result);
        }
    }

    private sealed class StubAddonCallbackController : Application.Abstractions.Actions.IAddonCallbackController
    {
        public Task<AddonCallbackIntResult> SendCallbackIntAsync(string addonName, int value, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(new AddonCallbackIntResult(addonName, value, true, null, "ok"));
        }

        public Task<AddonCallbackValuesResult> SendCallbackValuesAsync(string addonName, IReadOnlyList<int> values, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(new AddonCallbackValuesResult(addonName, [.. values], true, null, "ok"));
        }
    }
}
