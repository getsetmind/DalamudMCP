using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class InventorySummaryOperationTests
{
    [Fact]
    public void InventorySummaryOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(InventorySummaryOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("inventory.summary", operation.OperationId);
        Assert.Equal(["inventory", "summary"], cli.PathSegments);
        Assert.Equal("get_inventory_summary", mcp.Name);
    }

    [Fact]
    public void InventorySummaryOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(InventorySummaryOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("inventory.summary", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        InventorySummarySnapshot expected = new(
            DateTimeOffset.UtcNow,
            123456,
            87,
            140,
            new InventoryCategoryBreakdown(87, 13, 145, 5, 8),
            "87/140 main inventory slots occupied; 145 armory items, 13 equipped items, 5 currency entries, and 8 crystal stacks tracked (123456 gil tracked).");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        InventorySummaryOperation operation = new(
            cancellation =>
            {
                observedCancellationToken = cancellation;
                return ValueTask.FromResult(expected);
            });

        InventorySummarySnapshot actual = await operation.ExecuteAsync(
            new InventorySummaryOperation.Request(),
            OperationContext.ForCli("inventory.summary", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void Reader_status_reflects_internal_readiness()
    {
        InventorySummaryOperation operation = new(
            static _ => ValueTask.FromResult(new InventorySummarySnapshot(
                DateTimeOffset.UtcNow,
                0,
                0,
                140,
                new InventoryCategoryBreakdown(0, 0, 0, 0, 0),
                "0/140 main inventory slots occupied; 0 armory items, 0 equipped items, 0 currency entries, and 0 crystal stacks tracked (gil unavailable).")),
            isReady: false,
            detail: "not_logged_in");

        Assert.False(operation.IsReady);
        Assert.Equal("not_logged_in", operation.Detail);
    }
}



