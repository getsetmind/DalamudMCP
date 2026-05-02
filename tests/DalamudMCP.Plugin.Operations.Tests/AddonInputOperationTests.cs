using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonInputOperationTests
{
    [Fact]
    public void AddonInputOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonInputOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.input", operation.OperationId);
        Assert.Equal(["addon", "input"], cli.PathSegments);
        Assert.Equal("send_addon_input", mcp.Name);
    }

    [Fact]
    public void AddonInputOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonInputOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.input", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonInputResult expected = new("Inventory", "gamepad", 1, false, true, null, "Sent gamepad input 1 (down) to Inventory.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonInputOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("Inventory", request.AddonName);
                Assert.Equal("gamepad", request.InputType);
                Assert.Equal(1, request.InputId);
                Assert.Equal("down", request.InputState);
                return ValueTask.FromResult(expected);
            });

        AddonInputResult actual = await operation.ExecuteAsync(
            new AddonInputOperation.Request
            {
                AddonName = "Inventory",
                InputType = "gamepad",
                InputId = 1,
                InputState = "down"
            },
            OperationContext.ForCli("addon.input", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



