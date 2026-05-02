using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Plugin.Relay;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "plugin.data.poll",
    Description = "Polls buffered JSON data from a plugin data relay channel. max-items limits how many items are removed from the buffer; remaining items stay available for the next poll.",
    Summary = "Polls buffered data from a plugin data relay channel.")]
[ResultFormatter(typeof(PluginDataPollOperation.TextFormatter))]
[CliCommand("plugin", "data", "poll")]
[McpTool("plugin_data_poll")]
public sealed partial class PluginDataPollOperation
    : IOperation<PluginDataPollOperation.Request, PluginDataPollResult>
{
    private const int MaxItemsUpperLimit = 10000;

    private readonly Func<Request, CancellationToken, ValueTask<PluginDataPollResult>> executor;

    [SupportedOSPlatform("windows")]
    public PluginDataPollOperation(IPluginDataRelayService relay, IFramework framework)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(framework);

        executor = CreatePluginExecutor(relay, framework);
    }

    internal PluginDataPollOperation(Func<Request, CancellationToken, ValueTask<PluginDataPollResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<PluginDataPollResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.data.poll")]
    public sealed partial class Request
    {
        [Option("channel", Description = "Full relay channel name, such as MyPlugin.status.")]
        public string Channel { get; init; } = string.Empty;

        [Option("max-items", Description = "Maximum items to return. Remaining buffered items stay available for the next poll.", Required = false)]
        public int? MaxItems { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<PluginDataPollResult>
    {
        public string? FormatText(PluginDataPollResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<PluginDataPollResult>> CreatePluginExecutor(
        IPluginDataRelayService relay,
        IFramework framework)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ExecutePoll(relay, request);

            return await framework.RunOnFrameworkThread(() => ExecutePoll(relay, request)).ConfigureAwait(false);
        };
    }

    internal static PluginDataPollResult ExecutePoll(IPluginDataRelayService relay, Request request)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(request);

        string fullChannelName = string.IsNullOrWhiteSpace(request.Channel)
            ? throw new ArgumentException("channel is required.", nameof(request))
            : request.Channel.Trim();
        try
        {
            int? maxItems = NormalizeMaxItems(request.MaxItems);

            if (!relay.TryPoll(fullChannelName, maxItems, out IReadOnlyList<string> data))
            {
                return new PluginDataPollResult(
                    fullChannelName,
                    true,
                    "channel_not_found",
                    0,
                    [],
                    null,
                    $"Channel '{fullChannelName}' was not found. Subscribe first with plugin_data_subscribe.");
            }

            if (data.Count == 0)
            {
                return new PluginDataPollResult(
                    fullChannelName,
                    true,
                    "no_data",
                    0,
                    [],
                    null,
                    $"Channel '{fullChannelName}' has no new data.");
            }

            string[] items = data.ToArray();
            return new PluginDataPollResult(
                fullChannelName,
                true,
                "data_available",
                items.Length,
                items,
                null,
                $"Read {items.Length} item(s) from '{fullChannelName}'.");
        }
        catch (ArgumentException exception)
        {
            return new PluginDataPollResult(
                fullChannelName,
                false,
                "validation_failed",
                0,
                [],
                exception.Message,
                $"Poll validation failed: {exception.Message}");
        }
        catch (Exception exception)
        {
            return new PluginDataPollResult(
                fullChannelName,
                false,
                "poll_failed",
                0,
                [],
                exception.Message,
                $"Poll failed: {exception.Message}");
        }
    }

    private static int? NormalizeMaxItems(int? maxItems)
    {
        if (!maxItems.HasValue)
            return null;

        if (maxItems.Value <= 0)
            throw new ArgumentException("max-items must be greater than zero.", nameof(maxItems));

        return Math.Min(maxItems.Value, MaxItemsUpperLimit);
    }
}

[MemoryPackable]
public sealed partial record PluginDataPollResult(
    string FullChannelName,
    bool Success,
    string Status,
    int ItemCount,
    string[] Items,
    string? ErrorMessage,
    string SummaryText);
