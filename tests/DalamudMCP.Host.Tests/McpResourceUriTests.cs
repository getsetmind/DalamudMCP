namespace DalamudMCP.Host.Tests;

public sealed class McpResourceUriTests
{
    [Fact]
    public void TryNormalize_RejectsNonFfxivScheme()
    {
        var success = McpResourceUri.TryNormalize("https://example.com", out var normalizedUri);

        Assert.False(success);
        Assert.Equal(string.Empty, normalizedUri);
    }

    [Fact]
    public void TryMatchTemplate_MatchesAddonTreeTemplate()
    {
        var success = McpResourceUri.TryMatchTemplate(
            "ffxiv://ui/addon/Inventory/tree",
            "ffxiv://ui/addon/{addonName}/tree",
            out var routeValues);

        Assert.True(success);
        Assert.Equal("Inventory", routeValues["addonName"]);
    }
}
