using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class DutyContextOperationTests
{
    [Fact]
    public void DutyContextOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(DutyContextOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("duty.context", operation.OperationId);
        Assert.Equal(["duty", "context"], cli.PathSegments);
        Assert.Equal("get_duty_context", mcp.Name);
    }

    [Fact]
    public void DutyContextOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(DutyContextOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("duty.context", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        DutyContextSnapshot expected = new(
            777,
            "Territory#777",
            "duty",
            true,
            false,
            "Territory#777 is active.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        DutyContextOperation operation = new(
            cancellation =>
            {
                observedCancellationToken = cancellation;
                return ValueTask.FromResult(expected);
            });

        DutyContextSnapshot actual = await operation.ExecuteAsync(
            new DutyContextOperation.Request(),
            OperationContext.ForCli("duty.context", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void Reader_status_reflects_internal_readiness()
    {
        DutyContextOperation operation = new(
            static _ => ValueTask.FromResult(new DutyContextSnapshot(null, null, "world", false, false, "Not currently in duty.")),
            isReady: false,
            detail: "not_logged_in");

        Assert.False(operation.IsReady);
        Assert.Equal("not_logged_in", operation.Detail);
    }
}
