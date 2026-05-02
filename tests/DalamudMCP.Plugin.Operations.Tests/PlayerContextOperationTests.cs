using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class PlayerContextOperationTests
{
    [Fact]
    public void PlayerContextOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(PlayerContextOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("player.context", operation.OperationId);
        Assert.Equal(["player", "context"], cli.PathSegments);
        Assert.Equal("get_player_context", mcp.Name);
    }

    [Fact]
    public void PlayerContextOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(PlayerContextOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("player.context", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        PlayerContextSnapshot expected = new(
            "Test Adventurer",
            "ExampleWorld",
            "Dancer",
            100,
            "Sample Plaza",
            new PlayerPosition(1.0, 2.0, 3.0));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        PlayerContextOperation operation = new(
            cancellation =>
            {
                observedCancellationToken = cancellation;
                return ValueTask.FromResult(expected);
            });

        PlayerContextSnapshot actual = await operation.ExecuteAsync(
            new PlayerContextOperation.Request(),
            OperationContext.ForCli("player.context", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



