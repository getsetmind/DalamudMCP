using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Plugin.Relay;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "plugin.data.subscribe",
    Description = "Subscribes to a plugin data relay channel. Target plugins can push JSON strings through the Dalamud IPC callgate DalamudMCP.Relay.{plugin-name}.{channel}.",
    Summary = "Subscribes to a plugin data relay channel.")]
[ResultFormatter(typeof(PluginDataSubscribeOperation.TextFormatter))]
[CliCommand("plugin", "data", "subscribe")]
[McpTool("plugin_data_subscribe")]
public sealed partial class PluginDataSubscribeOperation
    : IOperation<PluginDataSubscribeOperation.Request, PluginDataSubscribeResult>
{
    private const int DefaultCapacity = 1000;

    private readonly Func<Request, CancellationToken, ValueTask<PluginDataSubscribeResult>> executor;

    [SupportedOSPlatform("windows")]
    public PluginDataSubscribeOperation(IPluginDataRelayService relay, IFramework framework)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(framework);

        executor = CreatePluginExecutor(relay, framework);
    }

    internal PluginDataSubscribeOperation(Func<Request, CancellationToken, ValueTask<PluginDataSubscribeResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<PluginDataSubscribeResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.data.subscribe")]
    public sealed partial class Request
    {
        [Option("plugin-name", Description = "Target plugin InternalName.")]
        public string PluginName { get; init; } = string.Empty;

        [Option("channel", Description = "Relay channel name without plugin prefix.")]
        public string Channel { get; init; } = string.Empty;

        [Option("capacity", Description = "Maximum buffered items before oldest items are dropped.", Required = false)]
        public int? Capacity { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<PluginDataSubscribeResult>
    {
        public string? FormatText(PluginDataSubscribeResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<PluginDataSubscribeResult>> CreatePluginExecutor(
        IPluginDataRelayService relay,
        IFramework framework)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ExecuteSubscribe(relay, request);

            return await framework.RunOnFrameworkThread(() => ExecuteSubscribe(relay, request)).ConfigureAwait(false);
        };
    }

    internal static PluginDataSubscribeResult ExecuteSubscribe(IPluginDataRelayService relay, Request request)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(request);

        string pluginName = string.IsNullOrWhiteSpace(request.PluginName)
            ? throw new ArgumentException("plugin-name is required.", nameof(request))
            : request.PluginName.Trim();
        string channelName = string.IsNullOrWhiteSpace(request.Channel)
            ? throw new ArgumentException("channel is required.", nameof(request))
            : request.Channel.Trim();
        int capacity = request.Capacity ?? DefaultCapacity;
        string fullChannelName = $"{pluginName}.{channelName}";

        try
        {
            if (relay.Subscribe(pluginName, channelName, capacity))
            {
                return new PluginDataSubscribeResult(
                    fullChannelName,
                    pluginName,
                    true,
                    "subscribe_success",
                    null,
                    $"Subscribed to '{fullChannelName}'. Push JSON via IPC callgate 'DalamudMCP.Relay.{fullChannelName}'.");
            }

            return new PluginDataSubscribeResult(
                fullChannelName,
                pluginName,
                true,
                "already_subscribed",
                null,
                $"Channel '{fullChannelName}' is already subscribed.");
        }
        catch (Exception exception)
        {
            return new PluginDataSubscribeResult(
                fullChannelName,
                pluginName,
                false,
                "subscribe_failed",
                exception.Message,
                $"Subscribe failed: {exception.Message}");
        }
    }
}

[MemoryPackable]
public sealed partial record PluginDataSubscribeResult(
    string FullChannelName,
    string PluginName,
    bool Success,
    string Status,
    string? ErrorMessage,
    string SummaryText);
