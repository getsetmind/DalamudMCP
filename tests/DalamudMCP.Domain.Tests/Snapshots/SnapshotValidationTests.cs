using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Domain.Tests.Snapshots;

public sealed class SnapshotValidationTests
{
    [Fact]
    public void PlayerContextSnapshot_RejectsBlankCharacterName()
    {
        var capturedAt = DateTimeOffset.UtcNow;

        var exception = Assert.Throws<ArgumentException>(() => new PlayerContextSnapshot(
            capturedAt,
            " ",
            "Tonberry",
            "Tonberry",
            24,
            "White Mage",
            100,
            1,
            "Limsa Lominsa",
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
            "Lv100 White Mage in Limsa."));

        Assert.Equal("CharacterName", exception.ParamName);
    }

    [Fact]
    public void DutyContextSnapshot_RejectsDefaultTimestamp()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DutyContextSnapshot(
            default,
            777,
            "The Praetorium",
            "Dungeon",
            true,
            false,
            "In duty"));

        Assert.Equal("CapturedAt", exception.ParamName);
    }

    [Fact]
    public void InventorySummarySnapshot_RejectsOccupiedSlotsGreaterThanTotalSlots()
    {
        var exception = Assert.Throws<ArgumentException>(() => new InventorySummarySnapshot(
            DateTimeOffset.UtcNow,
            CurrencyGil: 1_000,
            OccupiedSlots: 11,
            TotalSlots: 10,
            CategoryCounts: new Dictionary<string, int> { ["Inventory"] = 11 },
            SummaryText: "Too many items."));

        Assert.Equal("OccupiedSlots", exception.ParamName);
    }

    [Fact]
    public void AddonSummary_RejectsBlankAddonName()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AddonSummary(
            " ",
            IsReady: true,
            IsVisible: true,
            CapturedAt: DateTimeOffset.UtcNow,
            SummaryText: "Ready"));

        Assert.Equal("AddonName", exception.ParamName);
    }

    [Fact]
    public void AddonTreeSnapshot_RejectsNullRoots()
    {
        Assert.Throws<ArgumentNullException>(() => new AddonTreeSnapshot(
            AddonName: "MJIMahjong",
            CapturedAt: DateTimeOffset.UtcNow,
            Roots: null!));
    }

    [Fact]
    public void NodeSnapshot_RejectsNegativeWidth()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new NodeSnapshot(
            NodeId: 1,
            NodeType: "TextNode",
            Visible: true,
            X: 10,
            Y: 20,
            Width: -1,
            Height: 40,
            Text: "Hello",
            Children: []));

        Assert.Equal("Width", exception.ParamName);
    }

    [Fact]
    public void StringTableEntry_RejectsNegativeIndex()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new StringTableEntry(-1, "raw", "decoded"));

        Assert.Equal("Index", exception.ParamName);
    }

    [Fact]
    public void StringTableSnapshot_RejectsBlankAddonName()
    {
        var exception = Assert.Throws<ArgumentException>(() => new StringTableSnapshot(
            AddonName: "",
            CapturedAt: DateTimeOffset.UtcNow,
            Entries: [new StringTableEntry(0, "raw", "decoded")]));

        Assert.Equal("AddonName", exception.ParamName);
    }

    [Fact]
    public void PositionSnapshot_RejectsBlankPrecision()
    {
        var exception = Assert.Throws<ArgumentException>(() => new PositionSnapshot(1, 2, 3, ""));

        Assert.Equal("Precision", exception.ParamName);
    }
}
