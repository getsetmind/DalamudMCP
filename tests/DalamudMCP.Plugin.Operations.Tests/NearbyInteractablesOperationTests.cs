using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class NearbyInteractablesOperationTests
{
    [Fact]
    public void NearbyInteractablesOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(NearbyInteractablesOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("nearby.interactables", operation.OperationId);
        Assert.Equal(["nearby", "interactables"], cli.PathSegments);
        Assert.Equal("get_nearby_interactables", mcp.Name);
    }

    [Fact]
    public void NearbyInteractablesOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(NearbyInteractablesOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("nearby.interactables", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        NearbyInteractablesSnapshot expected = new(
            DateTimeOffset.UtcNow,
            8d,
            [
                new NearbyInteractable("0x123", "Summoning Bell", "EventObj", true, 3.5, 1.2, new NearbyInteractablePosition(1, 2, 3))
            ],
            "1 interactable objects within 8 yalms.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        NearbyInteractablesOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal(12d, request.MaxDistance);
                Assert.Equal("bell", request.NameContains);
                Assert.True(request.IncludePlayers);
                return ValueTask.FromResult(expected);
            });

        NearbyInteractablesSnapshot actual = await operation.ExecuteAsync(
            new NearbyInteractablesOperation.Request
            {
                MaxDistance = 12d,
                NameContains = "bell",
                IncludePlayers = true
            },
            OperationContext.ForCli("nearby.interactables", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



