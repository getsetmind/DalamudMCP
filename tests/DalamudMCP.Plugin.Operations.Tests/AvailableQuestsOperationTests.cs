using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class AvailableQuestsOperationTests
{
    [Fact]
    public void AvailableQuestsOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(AvailableQuestsOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("quest.available", operation.OperationId);
        Assert.Equal(["quest", "available"], cli.PathSegments);
        Assert.Equal("get_available_quests", mcp.Name);
    }

    [Fact]
    public void AvailableQuestsOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(AvailableQuestsOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("quest.available", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        AvailableQuestsSnapshot expected = new(
            DateTimeOffset.UtcNow,
            132,
            "weather",
            [
                new AvailableQuest(
                    999,
                    "Weather Report Wanted",
                    1,
                    new AvailableQuestMarker(
                        1,
                        12345,
                        123,
                        132,
                        0.5,
                        new AvailableQuestPosition(1.0, 2.0, 3.0)),
                    "Weather Report Wanted (999) is available in Territory#132 (level 1).")
            ],
            "1 visible unaccepted quests matching 'weather' found in Territory#132.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        AvailableQuestsOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("weather", request.NameContains);
                Assert.Equal(4, request.MaxResults);
                return ValueTask.FromResult(expected);
            });

        AvailableQuestsSnapshot actual = await operation.ExecuteAsync(
            new AvailableQuestsOperation.Request
            {
                NameContains = "weather",
                MaxResults = 4
            },
            OperationContext.ForCli("quest.available", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void Reader_status_reflects_internal_readiness()
    {
        AvailableQuestsOperation operation = new(
            static (_, _) => ValueTask.FromResult(new AvailableQuestsSnapshot(
                DateTimeOffset.UtcNow,
                132,
                null,
                [],
                "No visible unaccepted quests were found in Territory#132.")),
            isReady: false,
            detail: "not_logged_in");

        Assert.False(operation.IsReady);
        Assert.Equal("not_logged_in", operation.Detail);
    }
}
