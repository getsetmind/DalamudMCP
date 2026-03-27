using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonEventOperationTests
{
    [Fact]
    public void AddonEventOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonEventOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.event", operation.OperationId);
        Assert.Equal(["addon", "event"], cli.PathSegments);
        Assert.Equal("send_addon_event", mcp.Name);
    }

    [Fact]
    public void AddonEventOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonEventOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.event", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonEventResult expected = new("Inventory", "buttonClick", 7, 2, 40, true, null, "Dispatched buttonClick event 7 to Inventory via collision[2]; node dispatch handled it.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonEventOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("Inventory", request.AddonName);
                Assert.Equal("buttonClick", request.EventType);
                Assert.Equal(2, request.CollisionIndex);
                Assert.Equal(40, request.NodeId);
                return ValueTask.FromResult(expected);
            });

        AddonEventResult actual = await operation.ExecuteAsync(
            new AddonEventOperation.Request
            {
                AddonName = "Inventory",
                EventType = "buttonClick",
                EventParam = 7,
                CollisionIndex = 2,
                NodeId = 40
            },
            OperationContext.ForCli("addon.event", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}
