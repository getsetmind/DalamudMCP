using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class FateContextOperationTests
{
    [Fact]
    public void FateContextOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(FateContextOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("fate.context", operation.OperationId);
        Assert.Equal(["fate", "context"], cli.PathSegments);
        Assert.Equal("get_fate_context", mcp.Name);
    }

    [Fact]
    public void FateContextOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(FateContextOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("fate.context", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        FateContextSnapshot expected = new(
            DateTimeOffset.UtcNow,
            144,
            120d,
            [
                new FateSnapshot(12, "Test Fate", "Running", 100, 100, 50, 600, false, 11.2, new FatePosition(1, 2, 3), "Defeat foes", null)
            ],
            "1 FATEs within 120 yalms.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        FateContextOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal(150d, request.MaxDistance);
                Assert.Equal("test", request.NameContains);
                return ValueTask.FromResult(expected);
            });

        FateContextSnapshot actual = await operation.ExecuteAsync(
            new FateContextOperation.Request
            {
                MaxDistance = 150d,
                NameContains = "test"
            },
            OperationContext.ForCli("fate.context", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}