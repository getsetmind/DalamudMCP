using System.Diagnostics;

namespace DalamudMCP.Plugin.Hosting;

public sealed class PluginHostController : IDisposable
{
    private readonly PluginHostPathResolver pathResolver;
    private Process? hostProcess;

    public PluginHostController(PluginHostPathResolver pathResolver)
    {
        this.pathResolver = pathResolver;
    }

    public string? LastError { get; private set; }

    public HostLaunchResolution? LastResolution { get; private set; }

    public bool IsRunning
    {
        get
        {
            RefreshExitedProcess();
            return hostProcess is { HasExited: false };
        }
    }

    public HostLaunchResolution? PreviewConsoleLaunch() => pathResolver.TryResolveConsole();

    public HostLaunchResolution? PreviewHttpLaunch(int port) => pathResolver.TryResolveHttpServer(port);

    public bool TryStartConsole() =>
        TryStart(pathResolver.TryResolveConsole(), showConsole: true);

    public bool TryStartHttpServer(int port) =>
        TryStart(pathResolver.TryResolveHttpServer(port), showConsole: false);

    private bool TryStart(HostLaunchResolution? resolution, bool showConsole)
    {
        RefreshExitedProcess();
        LastError = null;

        if (hostProcess is { HasExited: false })
        {
            return true;
        }

        if (resolution is null)
        {
            LastError = "Host executable could not be resolved from the current plugin output.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = resolution.DotNetExecutable,
                WorkingDirectory = resolution.WorkingDirectory,
                UseShellExecute = showConsole,
                CreateNoWindow = !showConsole,
                WindowStyle = showConsole ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
                ErrorDialog = false,
            };
            foreach (var argument in resolution.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            hostProcess = Process.Start(startInfo);
            LastResolution = resolution;

            if (hostProcess is null)
            {
                LastError = "Host process could not be started.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            hostProcess = null;
            return false;
        }
    }

    public void Stop()
    {
        RefreshExitedProcess();
        if (hostProcess is null)
        {
            return;
        }

        try
        {
            hostProcess.Kill(entireProcessTree: true);
            hostProcess.WaitForExit(2000);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            hostProcess.Dispose();
            hostProcess = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void RefreshExitedProcess()
    {
        if (hostProcess is not { HasExited: true })
        {
            return;
        }

        hostProcess.Dispose();
        hostProcess = null;
    }
}
