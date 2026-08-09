using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using DalamudMCP.Protocol;
using Lumina.Excel.Sheets;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation("target.context", Description = "Gets the current hard target, including identity, position, and cast state.", Summary = "Gets the current target.")]
[ResultFormatter(typeof(TargetContextOperation.TextFormatter))]
[CliCommand("target", "context")]
[McpTool("get_target_context")]
public sealed partial class TargetContextOperation : IOperation<TargetContextOperation.Request, TargetContextSnapshot>
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly ITargetManager targetManager;

    [SupportedOSPlatform("windows")]
    public TargetContextOperation(IFramework framework, IClientState clientState, ITargetManager targetManager)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.targetManager = targetManager;
    }

    public async ValueTask<TargetContextSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (framework.IsInFrameworkUpdateThread)
            return ReadCore(clientState, targetManager);
        return await framework.RunOnFrameworkThread(() => ReadCore(clientState, targetManager)).ConfigureAwait(false);
    }

    [MemoryPackable]
    [ProtocolOperation("target.context")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<TargetContextSnapshot>
    {
        public string? FormatText(TargetContextSnapshot result, OperationContext context) => result.SummaryText;
    }

    private static TargetContextSnapshot ReadCore(IClientState clientState, ITargetManager targetManager)
    {
        IGameObject? target = targetManager.Target;
        if (!clientState.IsLoggedIn || target is null)
            return new TargetContextSnapshot(DateTimeOffset.UtcNow, false, null, "No current target.");

        TargetObjectSnapshot value = new(
            $"0x{target.GameObjectId:X}",
            target.EntityId,
            target.BaseId,
            target.Name.TextValue,
            target.ObjectKind.ToString(),
            target.IsTargetable,
            target.IsDead,
            target.Position.X,
            target.Position.Y,
            target.Position.Z,
            target is IBattleChara battle && battle.IsCasting,
            target is IBattleChara caster ? caster.CastActionType : (byte)0,
            target is IBattleChara casting ? casting.CastActionId : 0,
            target is IBattleChara castTarget ? $"0x{castTarget.CastTargetObjectId:X}" : null,
            target is IBattleChara castTime ? castTime.CurrentCastTime : 0,
            target is IBattleChara totalCast ? totalCast.TotalCastTime : 0);
        return new TargetContextSnapshot(DateTimeOffset.UtcNow, true, value, $"Current target: {value.Name} ({value.GameObjectId}).");
    }
}

[Operation("party.context", Description = "Gets the current party or alliance member snapshot.", Summary = "Gets party members.")]
[ResultFormatter(typeof(PartyContextOperation.TextFormatter))]
[CliCommand("party", "context")]
[McpTool("get_party_context")]
public sealed partial class PartyContextOperation : IOperation<PartyContextOperation.Request, PartyContextSnapshot>
{
    private readonly IFramework framework;
    private readonly IPartyList partyList;

    [SupportedOSPlatform("windows")]
    public PartyContextOperation(IFramework framework, IPartyList partyList)
    {
        this.framework = framework;
        this.partyList = partyList;
    }

    public async ValueTask<PartyContextSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (framework.IsInFrameworkUpdateThread)
            return ReadCore(partyList);
        return await framework.RunOnFrameworkThread(() => ReadCore(partyList)).ConfigureAwait(false);
    }

    [MemoryPackable]
    [ProtocolOperation("party.context")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<PartyContextSnapshot>
    {
        public string? FormatText(PartyContextSnapshot result, OperationContext context) => result.SummaryText;
    }

    private static PartyContextSnapshot ReadCore(IPartyList partyList)
    {
        List<PartyMemberSnapshot> members = new(partyList.Length);
        for (int index = 0; index < partyList.Length; index++)
        {
            var member = partyList[index];
            if (member is null)
                continue;

            members.Add(new PartyMemberSnapshot(
                index,
                index == partyList.PartyLeaderIndex,
                member.ContentId,
                $"0x{member.EntityId:X}",
                member.Name.TextValue,
                member.World.RowId,
                member.ClassJob.RowId,
                member.Level,
                member.CurrentHP,
                member.MaxHP,
                member.CurrentMP,
                member.MaxMP,
                member.Position.X,
                member.Position.Y,
                member.Position.Z));
        }

        return new PartyContextSnapshot(
            DateTimeOffset.UtcNow,
            (ulong)partyList.PartyId,
            partyList.IsAlliance,
            members.ToArray(),
            $"{members.Count} party or alliance member(s) returned.");
    }
}

[Operation("condition.context", Description = "Gets every currently active Dalamud condition flag.", Summary = "Gets active condition flags.")]
[ResultFormatter(typeof(ConditionContextOperation.TextFormatter))]
[CliCommand("condition", "context")]
[McpTool("get_condition_context")]
public sealed partial class ConditionContextOperation(ICondition condition)
    : IOperation<ConditionContextOperation.Request, ConditionContextSnapshot>
{
    public ValueTask<ConditionContextSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        string[] flags = condition.AsReadOnlySet()
            .Select(static flag => flag.ToString())
            .OrderBy(static flag => flag, StringComparer.Ordinal)
            .ToArray();
        return ValueTask.FromResult(new ConditionContextSnapshot(
            DateTimeOffset.UtcNow,
            flags,
            $"{flags.Length} condition flag(s) are active."));
    }

    [MemoryPackable]
    [ProtocolOperation("condition.context")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<ConditionContextSnapshot>
    {
        public string? FormatText(ConditionContextSnapshot result, OperationContext context) => result.SummaryText;
    }
}

[Operation("inventory.items", Description = "Lists inventory items with container filters and cursor pagination.", Summary = "Lists inventory items.")]
[ResultFormatter(typeof(InventoryItemsOperation.TextFormatter))]
[CliCommand("inventory", "items")]
[McpTool("get_inventory_items")]
public sealed partial class InventoryItemsOperation : IOperation<InventoryItemsOperation.Request, InventoryItemsSnapshot>
{
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly IGameInventory inventory;

    [SupportedOSPlatform("windows")]
    public InventoryItemsOperation(IFramework framework, IClientState clientState, IGameInventory inventory, IDataManager dataManager)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.inventory = inventory;
        this.dataManager = dataManager;
    }

    public async ValueTask<InventoryItemsSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        GameInventoryType[] containers = InventoryOperationReader.ParseContainers(request.Containers);
        if (framework.IsInFrameworkUpdateThread)
            return InventoryOperationReader.Read(clientState, inventory, dataManager, containers, request.Cursor ?? 0, request.Limit ?? 100);
        return await framework.RunOnFrameworkThread(() =>
                InventoryOperationReader.Read(clientState, inventory, dataManager, containers, request.Cursor ?? 0, request.Limit ?? 100))
            .ConfigureAwait(false);
    }

    [MemoryPackable]
    [ProtocolOperation("inventory.items")]
    public sealed partial class Request
    {
        [Option("containers", Description = "Inventory container names. Empty selects the four main inventory bags.", Required = false)]
        public string[]? Containers { get; init; }

        [Option("cursor", Description = "Zero-based result offset.", Required = false)]
        public int? Cursor { get; init; }

        [Option("limit", Description = "Maximum rows to return (up to 200).", Required = false)]
        public int? Limit { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<InventoryItemsSnapshot>
    {
        public string? FormatText(InventoryItemsSnapshot result, OperationContext context) => result.SummaryText;
    }
}

[Operation("inventory.equipment", Description = "Gets currently equipped item slots.", Summary = "Gets equipped items.")]
[ResultFormatter(typeof(InventoryEquipmentOperation.TextFormatter))]
[CliCommand("inventory", "equipment")]
[McpTool("get_inventory_equipment")]
public sealed partial class InventoryEquipmentOperation(
    IFramework framework,
    IClientState clientState,
    IGameInventory inventory,
    IDataManager dataManager)
    : IOperation<InventoryEquipmentOperation.Request, InventoryItemsSnapshot>
{
    public async ValueTask<InventoryItemsSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (framework.IsInFrameworkUpdateThread)
            return InventoryOperationReader.Read(clientState, inventory, dataManager, [GameInventoryType.EquippedItems], 0, 200);
        return await framework.RunOnFrameworkThread(() =>
                InventoryOperationReader.Read(clientState, inventory, dataManager, [GameInventoryType.EquippedItems], 0, 200))
            .ConfigureAwait(false);
    }

    [MemoryPackable]
    [ProtocolOperation("inventory.equipment")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<InventoryItemsSnapshot>
    {
        public string? FormatText(InventoryItemsSnapshot result, OperationContext context) => result.SummaryText;
    }
}

[Operation("inventory.currencies", Description = "Gets tracked currency and crystal inventory entries.", Summary = "Gets currencies and crystals.")]
[ResultFormatter(typeof(InventoryCurrenciesOperation.TextFormatter))]
[CliCommand("inventory", "currencies")]
[McpTool("get_inventory_currencies")]
public sealed partial class InventoryCurrenciesOperation(
    IFramework framework,
    IClientState clientState,
    IGameInventory inventory,
    IDataManager dataManager)
    : IOperation<InventoryCurrenciesOperation.Request, InventoryItemsSnapshot>
{
    public async ValueTask<InventoryItemsSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        GameInventoryType[] containers = [GameInventoryType.Currency, GameInventoryType.Crystals];
        if (framework.IsInFrameworkUpdateThread)
            return InventoryOperationReader.Read(clientState, inventory, dataManager, containers, 0, 200);
        return await framework.RunOnFrameworkThread(() =>
                InventoryOperationReader.Read(clientState, inventory, dataManager, containers, 0, 200))
            .ConfigureAwait(false);
    }

    [MemoryPackable]
    [ProtocolOperation("inventory.currencies")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<InventoryItemsSnapshot>
    {
        public string? FormatText(InventoryItemsSnapshot result, OperationContext context) => result.SummaryText;
    }
}

internal static class InventoryOperationReader
{
    private static readonly GameInventoryType[] DefaultContainers =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4,
    ];

    public static GameInventoryType[] ParseContainers(string[]? values)
    {
        if (values is not { Length: > 0 })
            return DefaultContainers;

        return values
            .Select(static value => Enum.TryParse(value, true, out GameInventoryType parsed) ? parsed : (GameInventoryType?)null)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .Distinct()
            .ToArray();
    }

    public static InventoryItemsSnapshot Read(
        IClientState clientState,
        IGameInventory inventory,
        IDataManager dataManager,
        GameInventoryType[] containers,
        int cursor,
        int limit)
    {
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Inventory is unavailable because the player is not logged in.");
        int normalizedCursor = Math.Max(0, cursor);
        int normalizedLimit = limit <= 0 ? 100 : Math.Min(limit, 200);
        List<InventoryItemSnapshot> all = [];
        foreach (GameInventoryType container in containers)
        {
            foreach (GameInventoryItem item in inventory.GetInventoryItems(container))
            {
                if (item.IsEmpty)
                    continue;

                string? name = dataManager.GetExcelSheet<Item>()?.GetRowOrDefault(item.BaseItemId)?.Name.ToString();
                all.Add(new InventoryItemSnapshot(
                    container.ToString(),
                    item.InventorySlot,
                    item.ItemId,
                    item.BaseItemId,
                    name,
                    (uint)item.Quantity,
                    item.IsHq,
                    (ushort)item.Condition,
                    item.SpiritbondOrCollectability,
                    item.GlamourId));
            }
        }

        InventoryItemSnapshot[] page = all.Skip(normalizedCursor).Take(normalizedLimit).ToArray();
        int? nextCursor = normalizedCursor + page.Length < all.Count ? normalizedCursor + page.Length : null;
        return new InventoryItemsSnapshot(
            DateTimeOffset.UtcNow,
            page,
            all.Count,
            nextCursor,
            nextCursor.HasValue,
            $"{page.Length} of {all.Count} inventory item(s) returned.");
    }
}

[MemoryPackable]
public sealed partial record TargetObjectSnapshot(
    string GameObjectId,
    uint EntityId,
    uint DataId,
    string Name,
    string ObjectKind,
    bool IsTargetable,
    bool IsDead,
    float X,
    float Y,
    float Z,
    bool IsCasting,
    byte CastActionType,
    uint CastActionId,
    string? CastTargetObjectId,
    float CurrentCastTime,
    float TotalCastTime);

[MemoryPackable]
public sealed partial record TargetContextSnapshot(
    DateTimeOffset CapturedAt,
    bool HasTarget,
    TargetObjectSnapshot? Target,
    string SummaryText);

[MemoryPackable]
public sealed partial record PartyMemberSnapshot(
    int Index,
    bool IsLeader,
    ulong ContentId,
    string ObjectId,
    string Name,
    uint WorldId,
    uint ClassJobId,
    byte Level,
    uint CurrentHp,
    uint MaxHp,
    ushort CurrentMp,
    ushort MaxMp,
    float X,
    float Y,
    float Z);

[MemoryPackable]
public sealed partial record PartyContextSnapshot(
    DateTimeOffset CapturedAt,
    ulong PartyId,
    bool IsAlliance,
    PartyMemberSnapshot[] Members,
    string SummaryText);

[MemoryPackable]
public sealed partial record ConditionContextSnapshot(
    DateTimeOffset CapturedAt,
    string[] ActiveFlags,
    string SummaryText);

[MemoryPackable]
public sealed partial record InventoryItemSnapshot(
    string Container,
    uint Slot,
    uint ItemId,
    uint BaseItemId,
    string? Name,
    uint Quantity,
    bool IsHq,
    ushort Condition,
    uint SpiritbondOrCollectability,
    uint GlamourId);

[MemoryPackable]
public sealed partial record InventoryItemsSnapshot(
    DateTimeOffset CapturedAt,
    InventoryItemSnapshot[] Items,
    int TotalCount,
    int? NextCursor,
    bool Truncated,
    string SummaryText);
