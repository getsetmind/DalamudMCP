using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonTreeOperationTests
{
    [Fact]
    public void AddonTreeOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonTreeOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.tree", operation.OperationId);
        Assert.Equal(["addon", "tree"], cli.PathSegments);
        Assert.Equal("get_addon_tree", mcp.Name);
    }

    [Fact]
    public void AddonTreeOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonTreeOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.tree", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonTreeSnapshot expected = new(
            "Inventory",
            DateTimeOffset.UtcNow,
            [new AddonTreeNode(1, "addon", true, 0, 0, 100, 100, "Inventory", [])]);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonTreeOperation operation = new((request, cancellation) =>
        {
            observedCancellationToken = cancellation;
            Assert.Equal("Inventory", request.AddonName);
            return ValueTask.FromResult(expected);
        });

        AddonTreeSnapshot actual = await operation.ExecuteAsync(
            new AddonTreeOperation.Request { AddonName = "Inventory" },
            OperationContext.ForCli("addon.tree", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}
