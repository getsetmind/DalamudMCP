using System.Runtime.Versioning;
using Dalamud.Game.Text;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "chat.read",
    Description = "Reads recent chat, combat, and system log entries captured by DalamudMCP.",
    Summary = "Gets recent chat log entries.")]
[ResultFormatter(typeof(ChatLogReadOperation.TextFormatter))]
[CliCommand("chat", "read")]
[McpTool("get_chat_log")]
public sealed partial class ChatLogReadOperation
    : IOperation<ChatLogReadOperation.Request, ChatLogSnapshot>, IPluginReaderStatus
{
    private readonly Func<Request, CancellationToken, ValueTask<ChatLogSnapshot>> executor;
    private readonly Func<bool> isReadyProvider;
    private readonly Func<string> detailProvider;

    [SupportedOSPlatform("windows")]
    public ChatLogReadOperation(ChatLogBufferService logBuffer)
    {
        ArgumentNullException.ThrowIfNull(logBuffer);

        executor = CreateExecutor(logBuffer);
        isReadyProvider = static () => true;
        detailProvider = static () => "ready";
    }

    internal ChatLogReadOperation(
        Func<Request, CancellationToken, ValueTask<ChatLogSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "chat.read";

    public bool IsReady => isReadyProvider();

    public string Detail => detailProvider();

    public ValueTask<ChatLogSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("chat.read")]
    [LegacyBridgeRequest("ReadChatLog")]
    public sealed partial class Request
    {
        [Option("channels", Description = "Chat channels to filter by, such as Say, Party, or System. Empty means all.", Required = false)]
        public string[]? Channels { get; init; }

        [Option("since", Description = "Only return entries at or after this UTC timestamp.", Required = false)]
        public DateTimeOffset? Since { get; init; }

        [Option("max-count", Description = "Maximum number of entries to return.", Required = false)]
        public int? MaxCount { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<ChatLogSnapshot>
    {
        public string? FormatText(ChatLogSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    private static Func<Request, CancellationToken, ValueTask<ChatLogSnapshot>> CreateExecutor(
        ChatLogBufferService logBuffer)
    {
        return (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            XivChatType[]? channelFilter = ParseChannels(request.Channels);
            int maxCount = request.MaxCount is > 0
                ? Math.Min(request.MaxCount.Value, 500)
                : 100;
            IReadOnlyList<ChatLogEntry> entries = logBuffer.GetRecent(channelFilter, request.Since, maxCount);

            return ValueTask.FromResult(new ChatLogSnapshot(
                DateTimeOffset.UtcNow,
                entries.ToArray(),
                entries.Count,
                $"{entries.Count} log entries returned."));
        };
    }

    internal static XivChatType[]? ParseChannels(string[]? channels)
    {
        if (channels is not { Length: > 0 })
            return null;

        List<XivChatType> parsedChannels = new(channels.Length);
        foreach (string channel in channels)
        {
            if (Enum.TryParse(channel, ignoreCase: true, out XivChatType parsed))
                parsedChannels.Add(parsed);
        }

        return parsedChannels.Count > 0 ? parsedChannels.ToArray() : null;
    }
}

[MemoryPackable]
public sealed partial record ChatLogSnapshot(
    DateTimeOffset CapturedAt,
    ChatLogEntry[] Entries,
    int TotalFilteredCount,
    string SummaryText);
