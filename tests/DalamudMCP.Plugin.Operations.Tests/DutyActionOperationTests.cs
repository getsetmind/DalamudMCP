using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class DutyActionOperationTests
{
    [Fact]
    public void DutyActionOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(DutyActionOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("duty.action", operation.OperationId);
        Assert.Equal(["duty", "action"], cli.PathSegments);
        Assert.Equal("use_duty_action", mcp.Name);
    }

    [Fact]
    public void DutyActionOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(DutyActionOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("duty.action", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        DutyActionResult expected = new(1, true, null, 777u, "Executed duty action slot 1.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        DutyActionOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal(1, request.Slot);
                return ValueTask.FromResult(expected);
            });

        DutyActionResult actual = await operation.ExecuteAsync(
            new DutyActionOperation.Request { Slot = 1 },
            OperationContext.ForCli("duty.action", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}



