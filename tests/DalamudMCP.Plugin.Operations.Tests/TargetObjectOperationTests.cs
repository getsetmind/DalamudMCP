using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class TargetObjectOperationTests
{
    [Fact]
    public void TargetObjectOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(TargetObjectOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("target.object", operation.OperationId);
        Assert.Equal(["target", "object"], cli.PathSegments);
        Assert.Equal("target_object", mcp.Name);
    }

    [Fact]
    public void TargetObjectOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(TargetObjectOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("target.object", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        TargetObjectResult expected = new("0x123", true, null, "0x123", "Summoning Bell", "EventObj", "Targeted Summoning Bell.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        TargetObjectOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("0x123", request.GameObjectId);
                return ValueTask.FromResult(expected);
            });

        TargetObjectResult actual = await operation.ExecuteAsync(
            new TargetObjectOperation.Request { GameObjectId = "0x123" },
            OperationContext.ForCli("target.object", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



