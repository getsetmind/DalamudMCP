using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class MoveToNearbyInteractableOperationTests
{
    [Fact]
    public void MoveToNearbyInteractableOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(MoveToNearbyInteractableOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("move.to.nearby.interactable", operation.OperationId);
        Assert.Equal(["move", "to", "nearby", "interactable"], cli.PathSegments);
        Assert.Equal("move_to_nearby_interactable", mcp.Name);
    }

    [Fact]
    public void MoveToNearbyInteractableOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(MoveToNearbyInteractableOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("move.to.nearby.interactable", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        MoveToEntityResult expected = new("bell", true, null, "0x123", "Summoning Bell", "EventObj", new MoveDestination(1, 2, 3), "Movement started toward Summoning Bell.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        MoveToNearbyInteractableOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("bell", request.NameContains);
                Assert.Equal(8d, request.MaxDistance);
                return ValueTask.FromResult(expected);
            });

        MoveToEntityResult actual = await operation.ExecuteAsync(
            new MoveToNearbyInteractableOperation.Request { NameContains = "bell", MaxDistance = 8d },
            OperationContext.ForCli("move.to.nearby.interactable", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}
