using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class CurrentQuestObjectiveOperationTests
{
    [Fact]
    public void CurrentQuestObjectiveOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(CurrentQuestObjectiveOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("quest.current-objective", operation.OperationId);
        Assert.Equal(["quest", "current-objective"], cli.PathSegments);
        Assert.Equal("get_current_quest_objective", mcp.Name);
    }

    [Fact]
    public void CurrentQuestObjectiveOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(CurrentQuestObjectiveOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("quest.current-objective", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        CurrentQuestObjectiveSnapshot expected = new(
            DateTimeOffset.UtcNow,
            132,
            999,
            "Weather Report Wanted",
            "normal",
            1,
            [
                new CurrentQuestObjectiveVisibleMarker(
                    999,
                    12345,
                    123,
                    132,
                    456,
                    789,
                    1,
                    0.5,
                    new CurrentQuestObjectivePosition(1.0, 2.0, 3.0),
                    "Quest Marker")
            ],
            [
                new CurrentQuestObjectiveLinkMarker(
                    "map",
                    999,
                    "Talk to the weather forecaster.",
                    1,
                    71234,
                    123,
                    123,
                    123)
            ],
            "Weather Report Wanted (999) is tracked at sequence 1; 1 visible objective markers found in Territory#132.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        CurrentQuestObjectiveOperation operation = new(
            cancellation =>
            {
                observedCancellationToken = cancellation;
                return ValueTask.FromResult(expected);
            });

        CurrentQuestObjectiveSnapshot actual = await operation.ExecuteAsync(
            new CurrentQuestObjectiveOperation.Request(),
            OperationContext.ForCli("quest.current-objective", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void Reader_status_reflects_internal_readiness()
    {
        CurrentQuestObjectiveOperation operation = new(
            static _ => ValueTask.FromResult(new CurrentQuestObjectiveSnapshot(
                DateTimeOffset.UtcNow,
                132,
                null,
                null,
                null,
                null,
                [],
                [],
                "No tracked quest objective is currently available in Territory#132.")),
            isReady: false,
            detail: "not_logged_in");

        Assert.False(operation.IsReady);
        Assert.Equal("not_logged_in", operation.Detail);
    }
}
