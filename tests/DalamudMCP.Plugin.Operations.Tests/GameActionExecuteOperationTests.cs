using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.Game;
using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GameActionExecuteOperationTests
{
    [Fact]
    public void Operation_carries_cli_mcp_and_protocol_metadata()
    {
        OperationAttribute? operation = typeof(GameActionExecuteOperation).GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = typeof(GameActionExecuteOperation).GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = typeof(GameActionExecuteOperation).GetCustomAttribute<McpToolAttribute>();
        ProtocolOperationAttribute? protocol = typeof(GameActionExecuteOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(cli);
        Assert.Equal("game.action.execute", operation?.OperationId);
        Assert.Equal(["game", "action", "execute"], cli.PathSegments);
        Assert.Equal("game_action_execute", mcp?.Name);
        Assert.Equal("game.action.execute", protocol?.OperationId);
    }

    [Theory]
    [InlineData(null, ActionType.Action)]
    [InlineData("generalaction", ActionType.GeneralAction)]
    [InlineData("PvPAction", ActionType.PvPAction)]
    public void TryParseActionType_accepts_defined_nonzero_values(string? value, ActionType expected)
    {
        Assert.True(GameActionExecuteOperation.TryParseActionType(value, out ActionType actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("999")]
    [InlineData("unknown")]
    public void TryParseActionType_rejects_unsupported_values(string value)
    {
        Assert.False(GameActionExecuteOperation.TryParseActionType(value, out _));
    }

    [Fact]
    public async Task ExecuteAsync_uses_injected_executor()
    {
        GameActionExecuteResult expected = new(
            true,
            true,
            null,
            "Action",
            7,
            7,
            "Test Action",
            "0xE0000000",
            false,
            true,
            0,
            "Accepted.");
        CancellationToken observedCancellationToken = default;
        GameActionExecuteOperation operation = new((request, cancellationToken) =>
        {
            Assert.Equal(7, request.ActionId);
            observedCancellationToken = cancellationToken;
            return ValueTask.FromResult(expected);
        });

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        GameActionExecuteResult actual = await operation.ExecuteAsync(
            new GameActionExecuteOperation.Request { ActionId = 7 },
            OperationContext.ForCli("game.action.execute", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual with { CorrelationId = null });
        Assert.NotNull(actual.CorrelationId);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_publishes_correlated_action_result_once()
    {
        GameEventBufferService events = new();
        GameActionExecuteResult accepted = new(
            true,
            true,
            null,
            "Action",
            7,
            7,
            "Test Action",
            "0xE0000000",
            false,
            true,
            0,
            "Accepted.");
        GameActionExecuteOperation operation = new(
            (_, _) => ValueTask.FromResult(accepted),
            events);

        GameActionExecuteResult result = await operation.ExecuteAsync(
            new GameActionExecuteOperation.Request { ActionId = 7 },
            OperationContext.ForCli("game.action.execute", cancellationToken: TestContext.Current.CancellationToken));
        GameEventQueryResult eventResult = events.Query();

        Assert.NotNull(result.CorrelationId);
        Assert.Single(eventResult.Entries);
        Assert.Equal("action.accepted", eventResult.Entries[0].Type);
        Assert.Equal(result.CorrelationId, eventResult.Entries[0].CorrelationId);
    }
}
