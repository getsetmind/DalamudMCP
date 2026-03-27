using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonListOperationTests
{
    [Fact]
    public void AddonListOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonListOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.list", operation.OperationId);
        Assert.Equal(["addon", "list"], cli.PathSegments);
        Assert.Equal("get_addon_list", mcp.Name);
    }

    [Fact]
    public void AddonListOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonListOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.list", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonSummary[] expected =
        [
            new AddonSummary("Inventory", true, true, DateTimeOffset.UtcNow, "Inventory is open and visible.")
        ];
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonListOperation operation = new(cancellation =>
        {
            observedCancellationToken = cancellation;
            return ValueTask.FromResult(expected);
        });

        AddonSummary[] actual = await operation.ExecuteAsync(
            new AddonListOperation.Request(),
            OperationContext.ForCli("addon.list", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}
