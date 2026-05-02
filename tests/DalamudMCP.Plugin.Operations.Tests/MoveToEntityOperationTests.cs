using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class MoveToEntityOperationTests
{
    [Fact]
    public void MoveToEntityOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(MoveToEntityOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("move.to.entity", operation.OperationId);
        Assert.Equal(["move", "to", "entity"], cli.PathSegments);
        Assert.Equal("move_to_entity", mcp.Name);
    }

    [Fact]
    public void MoveToEntityOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(MoveToEntityOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("move.to.entity", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        MoveToEntityResult expected = new("0x123", true, null, "0x123", "Summoning Bell", "EventObj", new MoveDestination(1, 2, 3), "Movement started toward Summoning Bell.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        MoveToEntityOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("0x123", request.GameObjectId);
                Assert.True(request.AllowFlight);
                return ValueTask.FromResult(expected);
            });

        MoveToEntityResult actual = await operation.ExecuteAsync(
            new MoveToEntityOperation.Request { GameObjectId = "0x123", AllowFlight = true },
            OperationContext.ForCli("move.to.entity", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}