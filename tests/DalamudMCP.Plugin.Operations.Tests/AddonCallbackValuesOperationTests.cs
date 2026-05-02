using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonCallbackValuesOperationTests
{
    [Fact]
    public void AddonCallbackValuesOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonCallbackValuesOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.callback.values", operation.OperationId);
        Assert.Equal(["addon", "callback", "values"], cli.PathSegments);
        Assert.Equal("send_addon_callback_values", mcp.Name);
    }

    [Fact]
    public void AddonCallbackValuesOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonCallbackValuesOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.callback.values", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonCallbackValuesResult expected = new("Inventory", [8, 1], true, null, "Sent callback values [8, 1] to Inventory.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonCallbackValuesOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("Inventory", request.AddonName);
                Assert.Equal([8, 1], request.Values);
                return ValueTask.FromResult(expected);
            });

        AddonCallbackValuesResult actual = await operation.ExecuteAsync(
            new AddonCallbackValuesOperation.Request { AddonName = "Inventory", Values = [8, 1] },
            OperationContext.ForCli("addon.callback.values", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}