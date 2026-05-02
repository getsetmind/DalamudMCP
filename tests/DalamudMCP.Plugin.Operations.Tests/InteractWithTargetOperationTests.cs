using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class InteractWithTargetOperationTests
{
    [Fact]
    public void InteractWithTargetOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(InteractWithTargetOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("interact.with.target", operation.OperationId);
        Assert.Equal(["interact", "with", "target"], cli.PathSegments);
        Assert.Equal("interact_with_target", mcp.Name);
    }

    [Fact]
    public void InteractWithTargetOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(InteractWithTargetOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("interact.with.target", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        InteractWithTargetResult expected = new("0x123", true, null, "0x123", "Summoning Bell", "EventObj", 2.3, "Interaction started with Summoning Bell.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        InteractWithTargetOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("0x123", request.ExpectedGameObjectId);
                Assert.True(request.CheckLineOfSight);
                return ValueTask.FromResult(expected);
            });

        InteractWithTargetResult actual = await operation.ExecuteAsync(
            new InteractWithTargetOperation.Request { ExpectedGameObjectId = "0x123", CheckLineOfSight = true },
            OperationContext.ForCli("interact.with.target", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



