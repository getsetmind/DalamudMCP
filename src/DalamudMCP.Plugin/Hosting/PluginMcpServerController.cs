using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DalamudMCP.Plugin.Hosting;

public sealed class PluginMcpServerController : IDisposable
{
    private const string InitializeProbeBody =
        "{\"jsonrpc\":\"2.0\",\"id\":\"probe-init\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"DalamudMCP.Plugin\",\"version\":\"1.0.0\"}}}";
    private const string ToolsListProbeBody =
        "{\"jsonrpc\":\"2.0\",\"id\":\"probe-list\",\"method\":\"tools/list\",\"params\":{}}";

    private static readonly HttpClient ProbeHttpClient = new()
    {
        Timeout = TimeSpan.FromMilliseconds(200)
    };

    private static readonly TimeSpan ProbeRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly PluginCliPathResolver pathResolver;
    private readonly Func<Uri, EndpointProbeResult> probeEndpoint;
    private readonly Func<int, bool> tryTerminateProcessByPort;
    private readonly object syncRoot = new();
    private readonly string defaultEndpointUrl = $"http://127.0.0.1:{CliDefaults.HttpPort}{CliDefaults.HttpPath}";

    private Process? process;
    private StringBuilder? standardError;
    private StringBuilder? standardOutput;
    private bool cachedEndpointAvailable;
    private Uri? endpointUri;
    private string? endpointUrl;
    private DateTimeOffset nextProbeAtUtc;
    private Task? probeTask;

    public PluginMcpServerController(
        PluginCliPathResolver pathResolver,
        IReadOnlyList<string> expectedMcpToolNames)
        : this(pathResolver, CreateEndpointProbe(() => expectedMcpToolNames), TryTerminateProcessByPort)
    {
    }

    public PluginMcpServerController(
        PluginCliPathResolver pathResolver,
        Func<IReadOnlyList<string>> expectedMcpToolNamesProvider)
        : this(pathResolver, CreateEndpointProbe(expectedMcpToolNamesProvider), TryTerminateProcessByPort)
    {
    }

    public PluginMcpServerController(PluginCliPathResolver pathResolver)
        : this(pathResolver, static endpoint => ProbeEndpoint(endpoint), TryTerminateProcessByPort)
    {
    }

    internal PluginMcpServerController(PluginCliPathResolver pathResolver, Func<Uri, bool> probeEndpoint)
        : this(
            pathResolver,
            endpoint => probeEndpoint(endpoint)
                ? EndpointProbeResult.Available()
                : EndpointProbeResult.Unavailable(),
            TryTerminateProcessByPort)
    {
    }

    internal PluginMcpServerController(
        PluginCliPathResolver pathResolver,
        Func<Uri, EndpointProbeResult> probeEndpoint,
        Func<int, bool> tryTerminateProcessByPort)
    {
        this.pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        this.probeEndpoint = probeEndpoint ?? throw new ArgumentNullException(nameof(probeEndpoint));
        this.tryTerminateProcessByPort = tryTerminateProcessByPort ?? throw new ArgumentNullException(nameof(tryTerminateProcessByPort));
        endpointUrl = defaultEndpointUrl;
        endpointUri = new Uri(defaultEndpointUrl, UriKind.Absolute);
    }

    public string EndpointUrl => endpointUrl ?? defaultEndpointUrl;

    public string? LastError { get; private set; }

    public string? LastCommandText => Resolution?.CommandText;

    public bool IsRunning
    {
        get
        {
            RefreshExitedProcess();
            if (process is { HasExited: false })
                return true;

            if (DateTimeOffset.UtcNow >= nextProbeAtUtc)
            {
                EndpointProbeResult probeResult = ProbeEndpointImmediately();
                if (probeResult.IsCurrent)
                    return true;
            }

            QueueEndpointProbeIfNeeded();
            return cachedEndpointAvailable;
        }
    }

    internal PluginMcpServerStatus GetStatus()
    {
        return new PluginMcpServerStatus(IsRunning, EndpointUrl, LastCommandText, LastError);
    }

    internal PluginCliLaunchResolution? Resolution { get; private set; }

    public bool Start()
    {
        RefreshExitedProcess();
        if (process is { HasExited: false })
            return true;

        EndpointProbeResult probeResult = ProbeEndpointImmediately();
        if (probeResult.IsCurrent)
            return true;

        if (probeResult.IsAvailable && !probeResult.MatchesExpectedCatalog)
            TryTerminateStaleEndpoint();

        LastError = null;
        IReadOnlyList<PluginCliLaunchResolution> resolutions = pathResolver.ResolveHttpServerCandidates(CliDefaults.HttpPort, CliDefaults.HttpPath);
        if (resolutions.Count == 0)
        {
            LastError = "The bundled CLI server executable could not be resolved.";
            return false;
        }

        List<string> attemptErrors = [];
        foreach (PluginCliLaunchResolution resolution in resolutions)
        {
            Resolution = resolution;
            UpdateEndpointCache(resolution.EndpointUrl);
            if (TryStart(resolution, out string? error))
                return true;

            if (!string.IsNullOrWhiteSpace(error))
                attemptErrors.Add($"{resolution.CommandText} => {error}");
        }

        if (attemptErrors.Count > 0)
            LastError = string.Join(" | ", attemptErrors);

        return false;
    }

    public void Stop()
    {
        RefreshExitedProcess();
        if (process is null)
            return;

        try
        {
            process.Kill(true);
            process.WaitForExit(2000);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            cachedEndpointAvailable = false;
            DetachProcess();
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    internal static EndpointProbeResult ProbeEndpoint(Uri endpoint)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(InitializeProbeBody, Encoding.UTF8, "application/json")
            };
            using HttpResponseMessage response = ProbeHttpClient.Send(request);
            if (response.StatusCode != HttpStatusCode.OK)
                return EndpointProbeResult.Unavailable();

            if (!response.Headers.TryGetValues("MCP-Protocol-Version", out IEnumerable<string>? protocolVersions) ||
                !protocolVersions.Contains("2025-03-26", StringComparer.Ordinal))
            {
                return EndpointProbeResult.Unavailable();
            }

            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return body.Contains("\"protocolVersion\":\"2025-03-26\"", StringComparison.Ordinal)
                ? EndpointProbeResult.Available()
                : EndpointProbeResult.Unavailable();
        }
        catch (HttpRequestException)
        {
            return EndpointProbeResult.Unavailable();
        }
        catch (TaskCanceledException)
        {
            return EndpointProbeResult.Unavailable();
        }
    }

    internal static bool TryReadToolNames(JsonDocument document, out HashSet<string>? toolNames)
    {
        ArgumentNullException.ThrowIfNull(document);

        toolNames = null;
        if (!document.RootElement.TryGetProperty("result", out JsonElement result) ||
            !result.TryGetProperty("tools", out JsonElement tools) ||
            tools.ValueKind is not JsonValueKind.Array)
        {
            return false;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonElement tool in tools.EnumerateArray())
        {
            if (!tool.TryGetProperty("name", out JsonElement name) ||
                name.ValueKind is not JsonValueKind.String)
            {
                return false;
            }

            string? value = name.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            names.Add(value);
        }

        toolNames = names;
        return true;
    }

    internal static bool TryTerminateProcessByPort(int port)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "netstat",
                Arguments = "-ano -p tcp",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using Process? process = Process.Start(startInfo);
            if (process is null)
                return false;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            if (!TryParseListeningProcessIds(output, port, out List<int> processIds))
                return false;

            bool killed = false;
            foreach (int processId in processIds)
            {
                if (processId == Environment.ProcessId)
                    continue;

                try
                {
                    using Process boundProcess = Process.GetProcessById(processId);
                    boundProcess.Kill(true);
                    boundProcess.WaitForExit(2000);
                    killed = true;
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            return killed;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryParseListeningProcessIds(string output, int port, out List<int> processIds)
    {
        ArgumentNullException.ThrowIfNull(output);

        processIds = [];
        using StringReader reader = new(output);
        while (reader.ReadLine() is { } line)
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 ||
                !string.Equals(parts[0], "TCP", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(parts[3], "LISTENING", StringComparison.OrdinalIgnoreCase) ||
                !HasPort(parts[1], port) ||
                !int.TryParse(parts[4], out int processId))
            {
                continue;
            }

            processIds.Add(processId);
        }

        return processIds.Count > 0;
    }

    private bool TryStart(PluginCliLaunchResolution resolution, out string? error)
    {
        error = null;

        ProcessStartInfo startInfo = new()
        {
            FileName = resolution.FileName,
            WorkingDirectory = resolution.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false
        };
        foreach (string argument in resolution.Arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            standardError = new StringBuilder();
            standardOutput = new StringBuilder();
            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += OnOutputDataReceived;
            process.ErrorDataReceived += OnErrorDataReceived;

            if (!process.Start())
            {
                error = "The MCP HTTP server process could not be started.";
                process.Dispose();
                process = null;
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (WaitForAvailability(process, new Uri(resolution.EndpointUrl, UriKind.Absolute)))
            {
                cachedEndpointAvailable = true;
                return true;
            }

            if (process.HasExited)
            {
                error = BuildExitError(process.ExitCode, standardError?.ToString(), standardOutput?.ToString());
                DetachProcess();
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            DetachProcess();
            return false;
        }
    }

    private bool WaitForAvailability(Process startedProcess, Uri endpoint)
    {
        const int attempts = 20;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (startedProcess.HasExited)
                return false;

            if (probeEndpoint(endpoint).IsCurrent)
                return true;

            Thread.Sleep(100);
        }

        return false;
    }

    private EndpointProbeResult ProbeEndpointImmediately()
    {
        Uri? endpoint = endpointUri;
        if (endpoint is null)
            return EndpointProbeResult.Unavailable();

        EndpointProbeResult result = probeEndpoint(endpoint);
        cachedEndpointAvailable = result.IsCurrent;
        if (result.IsCurrent)
        {
            LastError = null;
        }
        else if (result.IsAvailable && !string.IsNullOrWhiteSpace(result.Error))
        {
            LastError = result.Error;
        }

        nextProbeAtUtc = DateTimeOffset.UtcNow.Add(ProbeRefreshInterval);
        return result;
    }

    private void QueueEndpointProbeIfNeeded()
    {
        if (DateTimeOffset.UtcNow < nextProbeAtUtc)
            return;

        lock (syncRoot)
        {
            if (probeTask is { IsCompleted: false } || DateTimeOffset.UtcNow < nextProbeAtUtc)
                return;

            nextProbeAtUtc = DateTimeOffset.UtcNow.Add(ProbeRefreshInterval);
            probeTask = Task.Run(() =>
            {
                Uri? endpoint = endpointUri;
                cachedEndpointAvailable = endpoint is not null && probeEndpoint(endpoint).IsCurrent;
            });
        }
    }

    private void TryTerminateStaleEndpoint()
    {
        Uri? endpoint = endpointUri;
        if (endpoint is null)
            return;

        if (!tryTerminateProcessByPort(endpoint.Port))
            return;

        cachedEndpointAvailable = false;
        nextProbeAtUtc = DateTimeOffset.MinValue;
        Thread.Sleep(150);
    }

    private void RefreshExitedProcess()
    {
        if (process is not { HasExited: true })
            return;

        LastError = BuildExitError(process.ExitCode, standardError?.ToString(), standardOutput?.ToString());
        cachedEndpointAvailable = false;
        DetachProcess();
    }

    private void DetachProcess()
    {
        if (process is not null)
        {
            process.OutputDataReceived -= OnOutputDataReceived;
            process.ErrorDataReceived -= OnErrorDataReceived;
            process.Dispose();
        }

        process = null;
        standardError = null;
        standardOutput = null;
    }

    private void UpdateEndpointCache(string? endpoint)
    {
        string nextEndpoint = string.IsNullOrWhiteSpace(endpoint)
            ? defaultEndpointUrl
            : endpoint;
        if (string.Equals(endpointUrl, nextEndpoint, StringComparison.Ordinal))
            return;

        endpointUrl = nextEndpoint;
        endpointUri = new Uri(nextEndpoint, UriKind.Absolute);
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        AppendLine(standardOutput, eventArgs.Data);
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        AppendLine(standardError, eventArgs.Data);
    }

    private static void AppendLine(StringBuilder? builder, string? line)
    {
        if (builder is null || string.IsNullOrWhiteSpace(line))
            return;

        if (builder.Length > 0)
            builder.AppendLine();

        builder.Append(line.Trim());
    }

    private static string BuildExitError(int exitCode, string? standardError, string? standardOutput)
    {
        string? detail = FirstNonEmptyLine(standardError) ?? FirstNonEmptyLine(standardOutput);
        return string.IsNullOrWhiteSpace(detail)
            ? $"The MCP HTTP server process exited unexpectedly with code {exitCode}."
            : $"The MCP HTTP server process exited unexpectedly with code {exitCode}: {detail}";
    }

    private static string? FirstNonEmptyLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        using StringReader reader = new(value);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                return line.Trim();
        }

        return null;
    }

    private static Func<Uri, EndpointProbeResult> CreateEndpointProbe(Func<IReadOnlyList<string>> expectedMcpToolNamesProvider)
    {
        ArgumentNullException.ThrowIfNull(expectedMcpToolNamesProvider);
        return endpoint =>
        {
            IReadOnlyList<string> expectedMcpToolNames = expectedMcpToolNamesProvider();
            EndpointProbeResult availability = ProbeEndpoint(endpoint);
            if (!availability.IsAvailable || expectedMcpToolNames.Count == 0)
                return availability;

            HashSet<string> expected = new(expectedMcpToolNames, StringComparer.Ordinal);

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(ToolsListProbeBody, Encoding.UTF8, "application/json")
                };
                using HttpResponseMessage response = ProbeHttpClient.Send(request);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return availability with
                    {
                        MatchesExpectedCatalog = false,
                        Error = $"The MCP HTTP server endpoint at {endpoint} responded to initialize but rejected tools/list."
                    };
                }

                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using JsonDocument document = JsonDocument.Parse(body);
                if (!TryReadToolNames(document, out HashSet<string>? actual))
                {
                    return availability with
                    {
                        MatchesExpectedCatalog = false,
                        Error = $"The MCP HTTP server endpoint at {endpoint} returned an unreadable tools/list response."
                    };
                }

                return actual is not null && actual.SetEquals(expected)
                    ? availability
                    : availability with
                    {
                        MatchesExpectedCatalog = false,
                        Error = $"A stale MCP HTTP server is already bound to {endpoint}. Restart it from the current plugin instance."
                    };
            }
            catch (HttpRequestException)
            {
                return availability with
                {
                    MatchesExpectedCatalog = false,
                    Error = $"The MCP HTTP server endpoint at {endpoint} became unavailable during verification."
                };
            }
            catch (JsonException)
            {
                return availability with
                {
                    MatchesExpectedCatalog = false,
                    Error = $"The MCP HTTP server endpoint at {endpoint} returned invalid JSON for tools/list."
                };
            }
            catch (TaskCanceledException)
            {
                return availability with
                {
                    MatchesExpectedCatalog = false,
                    Error = $"The MCP HTTP server endpoint at {endpoint} timed out during verification."
                };
            }
        };
    }

    private static bool HasPort(string localEndpoint, int port)
    {
        int separatorIndex = localEndpoint.LastIndexOf(':');
        if (separatorIndex < 0 || separatorIndex == localEndpoint.Length - 1)
            return false;

        return int.TryParse(localEndpoint[(separatorIndex + 1)..], out int parsedPort) && parsedPort == port;
    }
}

internal sealed record PluginMcpServerStatus(
    bool IsRunning,
    string EndpointUrl,
    string? CommandText,
    string? LastError);

internal readonly record struct EndpointProbeResult(
    bool IsAvailable,
    bool MatchesExpectedCatalog,
    string? Error = null)
{
    public bool IsCurrent => IsAvailable && MatchesExpectedCatalog;

    public static EndpointProbeResult Available()
    {
        return new EndpointProbeResult(true, true);
    }

    public static EndpointProbeResult Unavailable()
    {
        return new EndpointProbeResult(false, false);
    }
}
