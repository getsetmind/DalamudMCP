using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "inventory.summary",
    Description = "Gets the current inventory summary.",
    Summary = "Gets inventory summary.")]
[ResultFormatter(typeof(InventorySummaryOperation.TextFormatter))]
[CliCommand("inventory", "summary")]
[McpTool("get_inventory_summary")]
public sealed partial class InventorySummaryOperation
    : IOperation<InventorySummaryOperation.Request, InventorySummarySnapshot>, IPluginReaderStatus
{
    private static readonly GameInventoryType[] MainInventoryContainers =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4
    ];

    private static readonly GameInventoryType[] EquippedContainers =
    [
        GameInventoryType.EquippedItems
    ];

    private static readonly GameInventoryType[] CurrencyContainers =
    [
        GameInventoryType.Currency
    ];

    private static readonly GameInventoryType[] CrystalContainers =
    [
        GameInventoryType.Crystals
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
        GameInventoryType.ArmorySoulCrystal
    ];

    private readonly Func<CancellationToken, ValueTask<InventorySummarySnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public InventorySummaryOperation(
        IFramework framework,
        IClientState clientState,
        IGameInventory gameInventory)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameInventory);

        executor = CreateDalamudExecutor(framework, clientState, gameInventory);
        isReadyProvider = () => clientState.IsLoggedIn;
        detailProvider = () => isReadyProvider() ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal InventorySummaryOperation(
        Func<CancellationToken, ValueTask<InventorySummarySnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "inventory.summary";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<InventorySummarySnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("inventory.summary")]
    [LegacyBridgeRequest("GetInventorySummary")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<InventorySummarySnapshot>
    {
        public string? FormatText(InventorySummarySnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<CancellationToken, ValueTask<InventorySummarySnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameInventory gameInventory)
    {
        return async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, gameInventory, cancellationToken);

            return await framework.RunOnFrameworkThread(() =>
                    ReadCurrentCore(clientState, gameInventory, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    private static InventorySummarySnapshot ReadCurrentCore(
        IClientState clientState,
        IGameInventory gameInventory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Inventory summary is not available because the player is not logged in.");

        InventoryContainerSummary mainInventory = SummarizeContainers(gameInventory, MainInventoryContainers);
        InventoryContainerSummary equipped = SummarizeContainers(gameInventory, EquippedContainers);
        InventoryContainerSummary currency = SummarizeContainers(gameInventory, CurrencyContainers);
        InventoryContainerSummary crystals = SummarizeContainers(gameInventory, CrystalContainers);
        InventoryContainerSummary armory = SummarizeContainers(gameInventory, ArmoryContainers);
        int gil = TryResolveGil(gameInventory, CurrencyContainers);

        InventoryCategoryBreakdown categories = new(
            mainInventory.OccupiedSlots,
            equipped.OccupiedSlots,
            armory.OccupiedSlots,
            currency.OccupiedSlots,
            crystals.OccupiedSlots);

        return new InventorySummarySnapshot(
            DateTimeOffset.UtcNow,
            gil,
            mainInventory.OccupiedSlots,
            mainInventory.TotalSlots,
            categories,
            FormatSummary(
                mainInventory.OccupiedSlots,
                mainInventory.TotalSlots,
                gil,
                equipped.OccupiedSlots,
                armory.OccupiedSlots,
                currency.OccupiedSlots,
                crystals.OccupiedSlots));
    }

    private static InventoryContainerSummary SummarizeContainers(
        IGameInventory gameInventory,
        IEnumerable<GameInventoryType> containerTypes)
    {
        int occupiedSlots = 0;
        int totalSlots = 0;
        foreach (GameInventoryType containerType in containerTypes)
        {
            foreach (GameInventoryItem item in gameInventory.GetInventoryItems(containerType))
            {
                totalSlots++;
                if (!item.IsEmpty)
                    occupiedSlots++;
            }
        }

        return new InventoryContainerSummary(occupiedSlots, totalSlots);
    }

    private static int TryResolveGil(
        IGameInventory gameInventory,
        IEnumerable<GameInventoryType> containerTypes)
    {
        foreach (GameInventoryType containerType in containerTypes)
        {
            foreach (GameInventoryItem item in gameInventory.GetInventoryItems(containerType))
            {
                if (item.IsEmpty)
                    continue;

                if (item.ItemId == 1 || item.BaseItemId == 1)
                    return SafeConvertToInt(item.Quantity);
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

    private static string FormatSummary(
        int occupiedSlots,
        int totalSlots,
        int gil,
        int equippedCount,
        int armoryCount,
        int currencyEntryCount,
        int crystalStackCount)
    {
        string gilText = gil > 0
            ? $"{gil.ToString(CultureInfo.InvariantCulture)} gil tracked"
            : "gil unavailable";

        return
            $"{occupiedSlots.ToString(CultureInfo.InvariantCulture)}/{totalSlots.ToString(CultureInfo.InvariantCulture)} main inventory slots occupied; " +
            $"{armoryCount.ToString(CultureInfo.InvariantCulture)} armory items, " +
            $"{equippedCount.ToString(CultureInfo.InvariantCulture)} equipped items, " +
            $"{currencyEntryCount.ToString(CultureInfo.InvariantCulture)} currency entries, and " +
            $"{crystalStackCount.ToString(CultureInfo.InvariantCulture)} crystal stacks tracked ({gilText}).";
    }

    private readonly record struct InventoryContainerSummary(int OccupiedSlots, int TotalSlots);
}

[MemoryPackable]
public sealed partial record InventoryCategoryBreakdown(
    int MainInventory,
    int Equipped,
    int Armory,
    int CurrencyEntries,
    int CrystalStacks);

[MemoryPackable]
public sealed partial record InventorySummarySnapshot(
    DateTimeOffset CapturedAt,
    int CurrencyGil,
    int OccupiedSlots,
    int TotalSlots,
    InventoryCategoryBreakdown Categories,
    string SummaryText);