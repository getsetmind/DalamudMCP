using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Session;

namespace DalamudMCP.Plugin.Readers;

public sealed class PluginSessionStateReader : ISessionStateReader
{
    private readonly IClock clock;
    private readonly IFramework? framework;
    private readonly Func<bool> isBridgeServerRunning;
    private readonly string pipeName;
    private readonly IReadOnlyList<IPluginReaderDiagnostics> readerDiagnostics;

    public PluginSessionStateReader(
        IClock clock,
        string pipeName,
        Func<bool> isBridgeServerRunning,
        IEnumerable<IPluginReaderDiagnostics> readerDiagnostics,
        IFramework? framework = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(isBridgeServerRunning);
        ArgumentNullException.ThrowIfNull(readerDiagnostics);

        this.clock = clock;
        this.framework = framework;
        this.pipeName = pipeName.Trim();
        this.isBridgeServerRunning = isBridgeServerRunning;
        this.readerDiagnostics = readerDiagnostics.ToArray();
    }

    public Task<SessionState> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (framework is not null && !framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(cancellationToken));
    }

    private SessionState ReadCurrentCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isRunning = isBridgeServerRunning();
        var components = readerDiagnostics
            .Select(ReadComponentState)
            .ToArray();
        var readyCount = components.Count(static component => component.IsReady);
        var summary = $"{readyCount}/{components.Length} readers ready; bridge server {(isRunning ? "running" : "stopped")}.";

        return new SessionState(
            clock.UtcNow,
            pipeName,
            isRunning,
            components,
            summary);
    }

    private static SessionComponentState ReadComponentState(IPluginReaderDiagnostics diagnostics)
    {
        try
        {
            return new SessionComponentState(diagnostics.ComponentName, diagnostics.IsReady, diagnostics.Status);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("Not on main thread!", StringComparison.Ordinal))
        {
            return new SessionComponentState(diagnostics.ComponentName, false, "thread_restricted");
        }
    }
}
