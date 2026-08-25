using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;

namespace DalamudMCP.Plugin.Services;

[SupportedOSPlatform("windows")]
public sealed class GameStateEventMonitor : IDisposable
{
    private static readonly GameInventoryType[] InventoryContainers =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4,
        GameInventoryType.EquippedItems,
        GameInventoryType.Currency,
        GameInventoryType.Crystals,
    ];

    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly GameEventBufferService events;
    private readonly IFramework framework;
    private readonly IGameInventory inventory;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly ITargetManager targetManager;
    private string? castRevision;
    private string? conditionRevision;
    private bool disposed;
    private int frame;
    private string? inventoryRevision;
    private string? partyRevision;
    private string? targetRevision;
    private uint? territoryId;

    public GameStateEventMonitor(
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IPartyList partyList,
        IGameInventory inventory,
        GameEventBufferService events)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.condition = condition;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.partyList = partyList;
        this.inventory = inventory;
        this.events = events;
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed)
            return;

        try
        {
            ObserveTerritory();
            ObserveTargetAndCast();
            ObserveConditions();
            if (++frame % 30 == 0)
            {
                ObserveParty();
                ObserveInventory();
            }
        }
        catch
        {
            // An unavailable game-state pointer must not break the framework update chain.
        }
    }

    private void ObserveTerritory()
    {
        uint current = clientState.TerritoryType;
        if (territoryId == current)
            return;

        uint? previous = territoryId;
        territoryId = current;
        events.Publish("territory.changed", new { previousTerritoryId = previous, territoryId = current });
    }

    private void ObserveTargetAndCast()
    {
        IGameObject? target = targetManager.Target;
        string nextTargetRevision = target is null
            ? "none"
            : $"{target.GameObjectId:X}|{target.BaseId}|{target.ObjectKind}|{target.Name.TextValue}";
        if (!string.Equals(targetRevision, nextTargetRevision, StringComparison.Ordinal))
        {
            targetRevision = nextTargetRevision;
            events.Publish("target.changed", target is null
                ? new { hasTarget = false, gameObjectId = (string?)null, name = (string?)null, objectKind = (string?)null }
                : new
                {
                    hasTarget = true,
                    gameObjectId = (string?)$"0x{target.GameObjectId:X}",
                    name = (string?)target.Name.TextValue,
                    objectKind = (string?)target.ObjectKind.ToString(),
                });
        }

        IBattleChara? player = objectTable.LocalPlayer as IBattleChara;
        string nextCastRevision = player is null
            ? "none"
            : $"{player.IsCasting}|{player.CastActionType}|{player.CastActionId}|{player.CastTargetObjectId:X}";
        if (string.Equals(castRevision, nextCastRevision, StringComparison.Ordinal))
            return;

        castRevision = nextCastRevision;
        events.Publish("cast.changed", player is null
            ? new { isCasting = false, actionType = (byte)0, actionId = 0u, targetObjectId = (string?)null }
            : new
            {
                player.IsCasting,
                actionType = player.CastActionType,
                actionId = player.CastActionId,
                targetObjectId = $"0x{player.CastTargetObjectId:X}",
                player.CurrentCastTime,
                player.TotalCastTime,
            });
    }

    private void ObserveConditions()
    {
        string[] active = condition.AsReadOnlySet()
            .Select(static value => value.ToString())
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string nextRevision = string.Join('|', active);
        if (string.Equals(conditionRevision, nextRevision, StringComparison.Ordinal))
            return;

        conditionRevision = nextRevision;
        events.Publish("condition.changed", new { activeFlags = active });
    }

    private void ObserveParty()
    {
        List<object> members = new(partyList.Length);
        for (int index = 0; index < partyList.Length; index++)
        {
            var member = partyList[index];
            if (member is null)
                continue;
            members.Add(new
            {
                index,
                member.ContentId,
                entityId = member.EntityId,
                name = member.Name.TextValue,
                classJobId = member.ClassJob.RowId,
                member.Level,
                member.CurrentHP,
                member.MaxHP,
            });
        }

        string nextRevision = string.Join('|', members.Select(static value => value.ToString()));
        if (string.Equals(partyRevision, nextRevision, StringComparison.Ordinal))
            return;

        partyRevision = nextRevision;
        events.Publish("party.changed", new { partyList.PartyId, partyList.IsAlliance, members });
    }

    private void ObserveInventory()
    {
        int occupiedSlots = 0;
        ulong quantity = 0;
        foreach (GameInventoryType container in InventoryContainers)
        {
            foreach (GameInventoryItem item in inventory.GetInventoryItems(container))
            {
                if (item.IsEmpty)
                    continue;
                occupiedSlots++;
                quantity += Convert.ToUInt64(item.Quantity);
            }
        }

        string nextRevision = $"{occupiedSlots}|{quantity}";
        if (string.Equals(inventoryRevision, nextRevision, StringComparison.Ordinal))
            return;

        inventoryRevision = nextRevision;
        events.Publish("inventory.changed", new { occupiedSlots, totalQuantity = quantity });
    }
}
