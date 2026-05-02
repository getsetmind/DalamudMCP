using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Plugin.Relay;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "plugin.data.unsubscribe",
    Description = "Unsubscribes from a plugin data relay channel, unregistering the IPC endpoint and releasing the local buffer.",
    Summary = "Unsubscribes from a plugin data relay channel.")]
[ResultFormatter(typeof(PluginDataUnsubscribeOperation.TextFormatter))]
[CliCommand("plugin", "data", "unsubscribe")]
[McpTool("plugin_data_unsubscribe")]
public sealed partial class PluginDataUnsubscribeOperation
    : IOperation<PluginDataUnsubscribeOperation.Request, PluginDataUnsubscribeResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<PluginDataUnsubscribeResult>> executor;

    [SupportedOSPlatform("windows")]
    public PluginDataUnsubscribeOperation(IPluginDataRelayService relay, IFramework framework)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(framework);

        executor = CreatePluginExecutor(relay, framework);
    }

    internal PluginDataUnsubscribeOperation(Func<Request, CancellationToken, ValueTask<PluginDataUnsubscribeResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<PluginDataUnsubscribeResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.data.unsubscribe")]
    public sealed partial class Request
    {
        [Option("channel", Description = "Full relay channel name returned by plugin_data_subscribe.")]
        public string Channel { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<PluginDataUnsubscribeResult>
    {
        public string? FormatText(PluginDataUnsubscribeResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<PluginDataUnsubscribeResult>> CreatePluginExecutor(
        IPluginDataRelayService relay,
        IFramework framework)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ExecuteUnsubscribe(relay, request);

            return await framework.RunOnFrameworkThread(() => ExecuteUnsubscribe(relay, request)).ConfigureAwait(false);
        };
    }

    internal static PluginDataUnsubscribeResult ExecuteUnsubscribe(IPluginDataRelayService relay, Request request)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(request);

        string fullChannelName = string.IsNullOrWhiteSpace(request.Channel)
            ? throw new ArgumentException("channel is required.", nameof(request))
            : request.Channel.Trim();

        try
        {
            if (relay.Unsubscribe(fullChannelName))
            {
                return new PluginDataUnsubscribeResult(
                    fullChannelName,
                    true,
                    "unsubscribe_success",
                    null,
                    $"Unsubscribed from '{fullChannelName}'.");
            }

            return new PluginDataUnsubscribeResult(
                fullChannelName,
                true,
                "not_subscribed",
                null,
                $"Channel '{fullChannelName}' was not subscribed.");
        }
        catch (Exception exception)
        {
            return new PluginDataUnsubscribeResult(
                fullChannelName,
                false,
                "unsubscribe_failed",
                exception.Message,
                $"Unsubscribe failed: {exception.Message}");
        }
    }
}

[MemoryPackable]
public sealed partial record PluginDataUnsubscribeResult(
    string FullChannelName,
    bool Success,
    string Status,
    string? ErrorMessage,
    string SummaryText);
