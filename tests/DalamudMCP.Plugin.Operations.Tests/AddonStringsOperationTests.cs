using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AddonStringsOperationTests
{
    [Fact]
    public void AddonStringsOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AddonStringsOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("addon.strings", operation.OperationId);
        Assert.Equal(["addon", "strings"], cli.PathSegments);
        Assert.Equal("get_addon_strings", mcp.Name);
    }

    [Fact]
    public void AddonStringsOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AddonStringsOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("addon.strings", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AddonStringsSnapshot expected = new(
            "Inventory",
            DateTimeOffset.UtcNow,
            [new AddonStringEntry(0, "hello", "hello")]);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AddonStringsOperation operation = new((request, cancellation) =>
        {
            observedCancellationToken = cancellation;
            Assert.Equal("Inventory", request.AddonName);
            return ValueTask.FromResult(expected);
        });

        AddonStringsSnapshot actual = await operation.ExecuteAsync(
            new AddonStringsOperation.Request { AddonName = "Inventory" },
            OperationContext.ForCli("addon.strings", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



