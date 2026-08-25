using DalamudMCP.Plugin.Services;

namespace DalamudMCP.Plugin.Tests;

public sealed class GameEventBufferServiceTests
{
    [Fact]
    public void Query_uses_monotonic_cursors_filters_and_reports_truncation()
    {
        GameEventBufferService service = new();
        GameEventEntry first = service.Publish("target.changed", new { id = 1 })!;
        service.Publish("condition.changed", new { condition = "BoundByDuty" });
        GameEventEntry third = service.Publish("target.changed", new { id = 2 })!;

        GameEventQueryResult result = service.Query(first.Cursor, ["target.changed"], 1);

        Assert.Single(result.Entries);
        Assert.Equal(third.Cursor, result.NextCursor);
        Assert.False(result.Truncated);
        Assert.Contains("\"id\":2", result.Entries[0].DataJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuration_filters_types_and_bounds_capacity()
    {
        GameEventBufferService service = new();
        GameEventConfiguration configuration = service.Configure(["target.changed"], 1);

        Assert.Equal(16, configuration.Capacity);
        Assert.Null(service.Publish("condition.changed"));
        Assert.NotNull(service.Publish("target.changed"));
    }

    [Fact]
    public async Task Wait_returns_when_matching_event_arrives()
    {
        GameEventBufferService service = new();
        ValueTask<GameEventQueryResult> wait = service.WaitAsync(
            0,
            ["action.accepted"],
            10,
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        service.Publish("target.changed");
        service.Publish("action.accepted", new { actionId = 42 }, "correlation");
        GameEventQueryResult result = await wait;

        Assert.Single(result.Entries);
        Assert.Equal("action.accepted", result.Entries[0].Type);
        Assert.Equal("correlation", result.Entries[0].CorrelationId);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Wait_reports_timeout_without_blocking_a_thread()
    {
        GameEventBufferService service = new();

        GameEventQueryResult result = await service.WaitAsync(
            0,
            null,
            10,
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Entries);
        Assert.True(result.TimedOut);
    }
}
