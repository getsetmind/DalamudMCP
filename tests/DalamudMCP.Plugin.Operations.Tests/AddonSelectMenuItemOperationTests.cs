using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonSelectMenuItemOperationTests
{
    [Fact]
    public void AddonSelectMenuItemOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonSelectMenuItemOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.select.menu-item", operation.OperationId);
        Assert.Equal(["addon", "select", "menu-item"], cli.PathSegments);
        Assert.Equal("select_addon_menu_item", mcp.Name);
    }

    [Fact]
    public void AddonSelectMenuItemOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonSelectMenuItemOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.select.menu-item", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonSelectMenuItemResult expected = new(
            "SelectString",
            "都市転送網",
            "都市転送網",
            0,
            "popup-menu-callback",
            true,
            null,
            "Selected '都市転送網' from SelectString using callback index 0.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonSelectMenuItemOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("SelectString", request.AddonName);
                Assert.Equal("都市転送網", request.Label);
                Assert.False(request.ContainsMatch);
                return ValueTask.FromResult(expected);
            });

        AddonSelectMenuItemResult actual = await operation.ExecuteAsync(
            new AddonSelectMenuItemOperation.Request
            {
                AddonName = "SelectString",
                Label = "都市転送網"
            },
            OperationContext.ForCli("addon.select.menu-item", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void TryFindMenuItemMatch_prefers_exact_match_after_normalization()
    {
        AddonSelectMenuItemOperation.MenuItemCandidate[] candidates =
        [
            new("  ・都市転送網", 0, true),
            new("マーケット（革細工師ギルド前）", 1, true)
        ];

        AddonSelectMenuItemOperation.MenuItemMatch? match =
            AddonSelectMenuItemOperation.TryFindMenuItemMatch(candidates, "都市転送網", containsMatch: false);

        Assert.NotNull(match);
        Assert.Equal("  ・都市転送網", match!.Label);
        Assert.Equal(0, match.SelectionIndex);
    }

    [Fact]
    public void TryFindMenuItemMatch_ignores_control_characters_in_labels()
    {
        AddonSelectMenuItemOperation.MenuItemCandidate[] candidates =
        [
            new("\u0002\u0012\u0002F\u0003 都市転送網", 0, true)
        ];

        AddonSelectMenuItemOperation.MenuItemMatch? match =
            AddonSelectMenuItemOperation.TryFindMenuItemMatch(candidates, "都市転送網", containsMatch: false);

        Assert.NotNull(match);
        Assert.Equal(0, match!.SelectionIndex);
    }

    [Fact]
    public void TryFindMenuItemMatch_uses_contains_match_when_enabled()
    {
        AddonSelectMenuItemOperation.MenuItemCandidate[] candidates =
        [
            new("エーテライト・プラザ（冒険者・木工師ギルド/双蛇党）", 0, true),
            new("マーケット（革細工師ギルド前）", 1, true)
        ];

        AddonSelectMenuItemOperation.MenuItemMatch? match =
            AddonSelectMenuItemOperation.TryFindMenuItemMatch(candidates, "マーケット", containsMatch: true);

        Assert.NotNull(match);
        Assert.Equal("マーケット（革細工師ギルド前）", match!.Label);
        Assert.Equal(1, match.SelectionIndex);
    }

    [Fact]
    public void TryFindMenuItemMatch_skips_non_selectable_candidates()
    {
        AddonSelectMenuItemOperation.MenuItemCandidate[] candidates =
        [
            new("その他", -1, false),
            new("青狢門（中央森林東方面）", 7, true)
        ];

        AddonSelectMenuItemOperation.MenuItemMatch? match =
            AddonSelectMenuItemOperation.TryFindMenuItemMatch(candidates, "その他", containsMatch: true);

        Assert.Null(match);
    }

    [Fact]
    public void ExtractTelepotTownStringCandidates_skips_headers_and_assigns_selection_indices()
    {
        string[] labels =
        [
            "グリダニア：新市街",
            "現在地：エーテライト・プラザ（冒険者・木工師ギルド/双蛇党）",
            "直近の利用元：なし",
            "弓術士ギルド前",
            "グリダニア：旧市街",
            "マーケット（革細工師ギルド前）",
            "その他",
            "青狢門（中央森林東方面）"
        ];

        AddonSelectMenuItemOperation.MenuItemCandidate[] candidates =
            AddonSelectMenuItemOperation.ExtractTelepotTownStringCandidates(labels, maxSelectableCount: 4);

        Assert.Equal(
            [
                new AddonSelectMenuItemOperation.MenuItemCandidate("弓術士ギルド前", 0, true),
                new AddonSelectMenuItemOperation.MenuItemCandidate("グリダニア：旧市街", 1, true),
                new AddonSelectMenuItemOperation.MenuItemCandidate("マーケット（革細工師ギルド前）", 2, true),
                new AddonSelectMenuItemOperation.MenuItemCandidate("青狢門（中央森林東方面）", 3, true)
            ],
            candidates);
    }
}
