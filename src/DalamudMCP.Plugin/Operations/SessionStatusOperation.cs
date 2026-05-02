using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "session.status",
    Description = "Gets the current DalamudMCP session status.",
    Summary = "Gets session status.")]
[ResultFormatter(typeof(SessionStatusOperation.TextFormatter))]
[CliCommand("session", "status")]
[McpTool("get_session_status")]
public sealed partial class SessionStatusOperation
    : IOperation<SessionStatusOperation.Request, SessionStatusSnapshot>
{
    private readonly Func<CancellationToken, ValueTask<SessionStatusSnapshot>> executor;

    [SupportedOSPlatform("windows")]
    public SessionStatusOperation(
        IFramework framework,
        PluginRuntimeOptions options,
        NamedPipeProtocolServer protocolServer,
        IEnumerable<IPluginReaderStatus> readerStatuses)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(protocolServer);
        ArgumentNullException.ThrowIfNull(readerStatuses);
        executor = CreatePluginExecutor(framework, options, protocolServer, readerStatuses.ToArray());
    }

    internal SessionStatusOperation(Func<CancellationToken, ValueTask<SessionStatusSnapshot>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<SessionStatusSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("session.status")]
    [LegacyBridgeRequest("GetSessionStatus")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<SessionStatusSnapshot>
    {
        public string? FormatText(SessionStatusSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<CancellationToken, ValueTask<SessionStatusSnapshot>> CreatePluginExecutor(
        IFramework framework,
        PluginRuntimeOptions options,
        NamedPipeProtocolServer protocolServer,
        IReadOnlyList<IPluginReaderStatus> readerStatuses)
    {
        return async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return BuildSnapshot(options, protocolServer, readerStatuses, cancellationToken);

            return await framework.RunOnFrameworkThread(() =>
                    BuildSnapshot(options, protocolServer, readerStatuses, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    private static SessionStatusSnapshot BuildSnapshot(
        PluginRuntimeOptions options,
        NamedPipeProtocolServer protocolServer,
        IReadOnlyList<IPluginReaderStatus> readerStatuses,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SessionReaderStatus[] readers = new SessionReaderStatus[readerStatuses.Count];
        int readyCount = 0;
        for (int index = 0; index < readerStatuses.Count; index++)
        {
            SessionReaderStatus reader = CreateReaderStatus(readerStatuses[index]);
            readers[index] = reader;
            if (reader.IsReady)
                readyCount++;
        }

        bool protocolServerRunning = protocolServer.IsRunning;
        SessionRuntimeState state = protocolServerRunning switch
        {
            false when readyCount <= 0 => SessionRuntimeState.Unknown,
            false => SessionRuntimeState.Starting,
            true when readyCount >= readers.Length => SessionRuntimeState.Ready,
            _ => SessionRuntimeState.Degraded
        };

        string summary = $"{readyCount}/{readers.Length} readers ready; protocol server {(protocolServerRunning ? "running" : "stopped")} on {options.PipeName}.";
        return new SessionStatusSnapshot(state, summary, readers);
    }

    private static SessionReaderStatus CreateReaderStatus(IPluginReaderStatus reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            return new SessionReaderStatus(reader.ReaderKey, reader.IsReady, reader.Detail);
        }
        catch (InvalidOperationException exception)
        {
            return new SessionReaderStatus(GetReaderKey(reader), false, exception.Message);
        }
    }

    private static string GetReaderKey(IPluginReaderStatus reader)
    {
        try
        {
            return string.IsNullOrWhiteSpace(reader.ReaderKey)
                ? reader.GetType().Name
                : reader.ReaderKey;
        }
        catch
        {
            return reader.GetType().Name;
        }
    }
}

public enum SessionRuntimeState
{
    Unknown = 0,
    Starting = 1,
    Ready = 2,
    Degraded = 3
}

[MemoryPackable]
public sealed partial record SessionReaderStatus(
    string ReaderKey,
    bool IsReady,
    string? Detail = null);

[MemoryPackable]
public sealed partial record SessionStatusSnapshot(
    SessionRuntimeState State,
    string SummaryText,
    IReadOnlyList<SessionReaderStatus> Readers);



