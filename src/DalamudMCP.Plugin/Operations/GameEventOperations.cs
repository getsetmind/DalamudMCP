using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation("events.query", Description = "Returns buffered game events after a monotonic cursor.", Summary = "Queries buffered game events.")]
[ResultFormatter(typeof(EventsQueryOperation.TextFormatter))]
[CliCommand("events", "query")]
[McpTool("events_query")]
public sealed partial class EventsQueryOperation(GameEventBufferService events)
    : IOperation<EventsQueryOperation.Request, GameEventQueryResult>
{
    public ValueTask<GameEventQueryResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(events.Query(request.AfterCursor ?? 0, request.Types, request.Limit ?? 100));
    }

    [MemoryPackable]
    [ProtocolOperation("events.query")]
    public sealed partial class Request
    {
        [Option("after-cursor", Description = "Return events with a cursor greater than this value.", Required = false)]
        public long? AfterCursor { get; init; }

        [Option("types", Description = "Optional event type filters.", Required = false)]
        public string[]? Types { get; init; }

        [Option("limit", Description = "Maximum number of events to return (up to 500).", Required = false)]
        public int? Limit { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameEventQueryResult>
    {
        public string? FormatText(GameEventQueryResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("events.wait", Description = "Waits until a matching game event arrives after a cursor or the timeout expires.", Summary = "Waits for a game event.")]
[ResultFormatter(typeof(EventsWaitOperation.TextFormatter))]
[CliCommand("events", "wait")]
[McpTool("events_wait")]
public sealed partial class EventsWaitOperation(GameEventBufferService events)
    : IOperation<EventsWaitOperation.Request, GameEventQueryResult>
{
    public ValueTask<GameEventQueryResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return events.WaitAsync(
            request.AfterCursor ?? 0,
            request.Types,
            request.Limit ?? 100,
            TimeSpan.FromMilliseconds(request.TimeoutMilliseconds ?? 10000),
            context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("events.wait")]
    public sealed partial class Request
    {
        [Option("after-cursor", Description = "Wait for events with a cursor greater than this value.", Required = false)]
        public long? AfterCursor { get; init; }

        [Option("types", Description = "Optional event type filters.", Required = false)]
        public string[]? Types { get; init; }

        [Option("limit", Description = "Maximum number of events to return (up to 500).", Required = false)]
        public int? Limit { get; init; }

        [Option("timeout-milliseconds", Description = "Wait timeout in milliseconds (up to 30000).", Required = false)]
        public int? TimeoutMilliseconds { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameEventQueryResult>
    {
        public string? FormatText(GameEventQueryResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("events.configure", Description = "Configures enabled event types and the bounded event buffer capacity.", Summary = "Configures game events.")]
[ResultFormatter(typeof(EventsConfigureOperation.TextFormatter))]
[CliCommand("events", "configure")]
[McpTool("events_configure")]
public sealed partial class EventsConfigureOperation(GameEventBufferService events)
    : IOperation<EventsConfigureOperation.Request, GameEventConfiguration>
{
    public ValueTask<GameEventConfiguration> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(events.Configure(request.Types, request.Capacity));
    }

    [MemoryPackable]
    [ProtocolOperation("events.configure")]
    public sealed partial class Request
    {
        [Option("types", Description = "Enabled event types. Empty enables all types.", Required = false)]
        public string[]? Types { get; init; }

        [Option("capacity", Description = "Ring buffer capacity from 16 through 10000.", Required = false)]
        public int? Capacity { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameEventConfiguration>
    {
        public string? FormatText(GameEventConfiguration result, OperationContext context) =>
            $"Event buffer capacity is {result.Capacity}; {result.BufferedCount} event(s) are buffered.";
    }
}
