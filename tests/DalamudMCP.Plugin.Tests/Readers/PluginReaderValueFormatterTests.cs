using DalamudMCP.Plugin.Readers;

namespace DalamudMCP.Plugin.Tests.Readers;

public sealed class PluginReaderValueFormatterTests
{
    [Fact]
    public void FormatDutySummary_ReturnsDutyTextWhenInDuty()
    {
        var summary = PluginReaderValueFormatter.FormatDutySummary(true, "Territory#777", 777, false);

        Assert.Equal("Territory#777 is active.", summary);
    }

    [Fact]
    public void FormatAddonSummary_ReturnsVisibilityText()
    {
        var summary = PluginReaderValueFormatter.FormatAddonSummary("Character", true, false);

        Assert.Equal("Character is open and hidden.", summary);
    }

    [Fact]
    public void CreateAddonRootNode_UsesAddonShape()
    {
        var node = PluginReaderValueFormatter.CreateAddonRootNode(99, "Inventory", true, 10f, 20f, 300f, 400f);

        Assert.Equal(99, node.NodeId);
        Assert.Equal("addon", node.NodeType);
        Assert.True(node.Visible);
        Assert.Equal("Inventory", node.Text);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void CreateStringEntries_FiltersNullAndWhitespaceValues()
    {
        var entries = PluginReaderValueFormatter.CreateStringEntries([null, "", "Hello", 42, true]);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(2, entry.Index);
                Assert.Equal("Hello", entry.RawValue);
                Assert.Equal("Hello", entry.DecodedValue);
            },
            entry =>
            {
                Assert.Equal(3, entry.Index);
                Assert.Equal("42", entry.RawValue);
                Assert.Equal("42", entry.DecodedValue);
            },
            entry =>
            {
                Assert.Equal(4, entry.Index);
                Assert.Equal("true", entry.RawValue);
                Assert.Equal("true", entry.DecodedValue);
            });
    }

    [Fact]
    public void CreateInventoryCategoryCounts_ReturnsStableKeys()
    {
        var counts = PluginReaderValueFormatter.CreateInventoryCategoryCounts(77, 13, 42, 4, 3);

        Assert.Equal(77, counts["main_inventory"]);
        Assert.Equal(13, counts["equipped"]);
        Assert.Equal(42, counts["armory"]);
        Assert.Equal(4, counts["currency_entries"]);
        Assert.Equal(3, counts["crystal_stacks"]);
    }

    [Fact]
    public void FormatInventorySummary_IncludesOccupancyAndGil()
    {
        var summary = PluginReaderValueFormatter.FormatInventorySummary(
            occupiedSlots: 77,
            totalSlots: 140,
            gil: 123456,
            equippedCount: 13,
            armoryCount: 42,
            currencyEntryCount: 4,
            crystalStackCount: 3);

        Assert.Equal(
            "77/140 main inventory slots occupied; 42 armory items, 13 equipped items, 4 currency entries, and 3 crystal stacks tracked (123456 gil tracked).",
            summary);
    }
}
