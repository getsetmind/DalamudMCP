using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class QuestStatusOperationTests
{
    [Fact]
    public void QuestStatusOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(QuestStatusOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("quest.status", operation.OperationId);
        Assert.Equal(["quest", "status"], cli.PathSegments);
        Assert.Equal("get_quest_status", mcp.Name);
    }

    [Fact]
    public void QuestStatusOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(QuestStatusOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("quest.status", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        QuestStatusSnapshot expected = new(
            DateTimeOffset.UtcNow,
            "questId:42",
            [
                new QuestStatusEntrySnapshot(42, "Into the Light", true, false, 3, 100, "Into the Light (42) is accepted at sequence 3.")
            ],
            "1 quest entries matched 'questId:42'.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        QuestStatusOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal((uint)42, request.QuestId);
                Assert.Null(request.Query);
                Assert.Equal(4, request.MaxResults);
                return ValueTask.FromResult(expected);
            });

        QuestStatusSnapshot actual = await operation.ExecuteAsync(
            new QuestStatusOperation.Request
            {
                QuestId = 42,
                MaxResults = 4
            },
            OperationContext.ForCli("quest.status", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}
