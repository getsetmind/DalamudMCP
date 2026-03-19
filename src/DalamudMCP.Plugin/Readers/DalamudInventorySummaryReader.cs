using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudInventorySummaryReader : IInventorySummaryReader, IPluginReaderDiagnostics
{
    private static readonly GameInventoryType[] MainInventoryContainers =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4,
    ];

    private static readonly GameInventoryType[] EquippedContainers =
    [
        GameInventoryType.EquippedItems,
    ];

    private static readonly GameInventoryType[] CurrencyContainers =
    [
        GameInventoryType.Currency,
    ];

    private static readonly GameInventoryType[] CrystalContainers =
    [
        GameInventoryType.Crystals,
    ];

    private static readonly GameInventoryType[] ArmoryContainers =
    [
        GameInventoryType.ArmoryMainHand,
        GameInventoryType.ArmoryOffHand,
        GameInventoryType.ArmoryHead,
        GameInventoryType.ArmoryBody,
        GameInventoryType.ArmoryHands,
        GameInventoryType.ArmoryWaist,
        GameInventoryType.ArmoryLegs,
        GameInventoryType.ArmoryFeets,
        GameInventoryType.ArmoryEar,
        GameInventoryType.ArmoryNeck,
        GameInventoryType.ArmoryWrist,
        GameInventoryType.ArmoryRings,
        GameInventoryType.ArmorySoulCrystal,
    ];

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IGameInventory gameInventory;

    public DalamudInventorySummaryReader(
        IFramework framework,
        IClientState clientState,
        IGameInventory gameInventory)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameInventory);
        this.framework = framework;
        this.clientState = clientState;
        this.gameInventory = gameInventory;
    }

    public string ComponentName => "inventory_summary";

    public bool IsReady => clientState.IsLoggedIn;

    public string Status => IsReady ? "ready" : "not_logged_in";

    public Task<InventorySummarySnapshot?> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(cancellationToken));
    }

    private InventorySummarySnapshot? ReadCurrentCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return null;
        }

        var mainInventory = SummarizeContainers(MainInventoryContainers);
        var equipped = SummarizeContainers(EquippedContainers);
        var currency = SummarizeContainers(CurrencyContainers);
        var crystals = SummarizeContainers(CrystalContainers);
        var armory = SummarizeContainers(ArmoryContainers);
        var gil = TryResolveGil(CurrencyContainers);

        return new InventorySummarySnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            CurrencyGil: gil,
            OccupiedSlots: mainInventory.OccupiedSlots,
            TotalSlots: mainInventory.TotalSlots,
            CategoryCounts: PluginReaderValueFormatter.CreateInventoryCategoryCounts(
                mainInventory.OccupiedSlots,
                equipped.OccupiedSlots,
                armory.OccupiedSlots,
                currency.OccupiedSlots,
                crystals.OccupiedSlots),
            SummaryText: PluginReaderValueFormatter.FormatInventorySummary(
                mainInventory.OccupiedSlots,
                mainInventory.TotalSlots,
                gil,
                equipped.OccupiedSlots,
                armory.OccupiedSlots,
                currency.OccupiedSlots,
                crystals.OccupiedSlots));
    }

    private InventoryContainerSummary SummarizeContainers(IEnumerable<GameInventoryType> containerTypes)
    {
        var occupiedSlots = 0;
        var totalSlots = 0;
        foreach (var containerType in containerTypes)
        {
            foreach (var item in gameInventory.GetInventoryItems(containerType))
            {
                totalSlots++;
                if (!item.IsEmpty)
                {
                    occupiedSlots++;
                }
            }
        }

        return new InventoryContainerSummary(occupiedSlots, totalSlots);
    }

    private int TryResolveGil(IEnumerable<GameInventoryType> containerTypes)
    {
        foreach (var containerType in containerTypes)
        {
            foreach (var item in gameInventory.GetInventoryItems(containerType))
            {
                if (item.IsEmpty)
                {
                    continue;
                }

                if (item.ItemId == 1 || item.BaseItemId == 1)
                {
                    return SafeConvertToInt(item.Quantity);
                }
            }
        }

        return 0;
    }

    private static int SafeConvertToInt<T>(T value)
        where T : struct
    {
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private readonly record struct InventoryContainerSummary(int OccupiedSlots, int TotalSlots);
}
