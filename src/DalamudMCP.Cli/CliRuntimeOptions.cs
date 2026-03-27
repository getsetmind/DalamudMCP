namespace DalamudMCP.Cli;

public sealed class CliRuntimeOptions
{
    public const int DefaultHttpPort = 38473;
    public const string DefaultHttpPath = "/mcp";

    private CliRuntimeOptions(
        CliCommandMode mode,
        IReadOnlyList<string> commandArguments,
        string? pipeName,
        int httpPort,
        string httpPath)
    {
        Mode = mode;
        CommandArguments = commandArguments;
        PipeName = string.IsNullOrWhiteSpace(pipeName) ? null : pipeName.Trim();
        HttpPort = httpPort;
        HttpPath = NormalizeHttpPath(httpPath);
    }

    public CliCommandMode Mode { get; }

    public IReadOnlyList<string> CommandArguments { get; }

    public string? PipeName { get; }

    public int HttpPort { get; }

    public string HttpPath { get; }

    public CliRuntimeOptions WithPipeName(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        return new CliRuntimeOptions(Mode, [.. CommandArguments], pipeName, HttpPort, HttpPath);
    }

    public bool TryResolvePipeName(out string? pipeName, out string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(PipeName))
        {
            pipeName = PipeName;
            errorMessage = null;
            return true;
        }

        string? environmentPipeName = Environment.GetEnvironmentVariable("DALAMUD_MCP_PIPE");
        if (!string.IsNullOrWhiteSpace(environmentPipeName))
        {
            pipeName = environmentPipeName.Trim();
            errorMessage = null;
            return true;
        }

        if (Protocol.ProtocolClientDiscovery.TryRead(out Protocol.ProtocolClientDiscoveryRecord? discovery) &&
            discovery is not null)
        {
            pipeName = discovery.PipeName;
            errorMessage = null;
            return true;
        }

        pipeName = null;
        errorMessage = "No live plugin connection was found. Start the plugin, use the local MCP HTTP endpoint, or pass --pipe <name>.";
        return false;
    }

    public static string Usage =>
        """
        Usage:
          dalamudmcp [--pipe <name>] <operation> [arguments] [--json]
          dalamudmcp [--pipe <name>] serve mcp
          dalamudmcp [--pipe <name>] serve http [--port <number>] [--path <path>]
        """;

    public static bool TryParse(
        IReadOnlyList<string> args,
        out CliRuntimeOptions? options,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        List<string> remainingArgs = [.. args];
        string? pipeName;
        if (!TryExtractPipeName(remainingArgs, out pipeName, out errorMessage))
        {
            options = null;
            return false;
        }

        pipeName ??= Environment.GetEnvironmentVariable("DALAMUD_MCP_PIPE");
        if (errorMessage is not null)
        {
            options = null;
            return false;
        }

        if (remainingArgs.Count >= 2 &&
            string.Equals(remainingArgs[0], "serve", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(remainingArgs[1], "mcp", StringComparison.OrdinalIgnoreCase))
        {
            if (remainingArgs.Count > 2)
            {
                options = null;
                errorMessage = "The 'serve mcp' command does not accept additional arguments yet.";
                return false;
            }

            options = new CliRuntimeOptions(CliCommandMode.ServeMcp, [], pipeName, DefaultHttpPort, DefaultHttpPath);
            errorMessage = null;
            return true;
        }

        if (remainingArgs.Count >= 2 &&
            string.Equals(remainingArgs[0], "serve", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(remainingArgs[1], "http", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseServeHttpArguments(
                    [.. remainingArgs.Skip(2)],
                    out int httpPort,
                    out string httpPath,
                    out errorMessage))
            {
                options = null;
                return false;
            }

            options = new CliRuntimeOptions(CliCommandMode.ServeHttp, [], pipeName, httpPort, httpPath);
            errorMessage = null;
            return true;
        }

        options = new CliRuntimeOptions(CliCommandMode.DirectCli, remainingArgs, pipeName, DefaultHttpPort, DefaultHttpPath);
        errorMessage = null;
        return true;
    }

    private static bool TryExtractPipeName(List<string> args, out string? pipeName, out string? errorMessage)
    {
        pipeName = null;
        int index = 0;
        while (index < args.Count)
        {
            if (!string.Equals(args[index], "--pipe", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (index == args.Count - 1 || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                pipeName = null;
                errorMessage = "The --pipe option requires a non-empty value.";
                return false;
            }

            args.RemoveAt(index);
            string parsedPipeName = args[index];
            if (parsedPipeName.StartsWith("--", StringComparison.Ordinal))
            {
                pipeName = null;
                errorMessage = "The --pipe option requires a non-empty value.";
                return false;
            }

            pipeName = parsedPipeName;
            args.RemoveAt(index);
        }

        errorMessage = null;
        return true;
    }

    private static bool TryParseServeHttpArguments(
        List<string> args,
        out int port,
        out string path,
        out string? errorMessage)
    {
        port = DefaultHttpPort;
        path = DefaultHttpPath;

        int index = 0;
        while (index < args.Count)
        {
            if (string.Equals(args[index], "--port", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Count - 1 ||
                    !int.TryParse(args[index + 1], out int parsedPort) ||
                    parsedPort <= 0 ||
                    parsedPort > 65535)
                {
                    errorMessage = "The --port option requires an integer between 1 and 65535.";
                    return false;
                }

                port = parsedPort;
                args.RemoveAt(index);
                args.RemoveAt(index);
                continue;
            }

            if (string.Equals(args[index], "--path", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Count - 1 || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    errorMessage = "The --path option requires a non-empty value.";
                    return false;
                }

                path = NormalizeHttpPath(args[index + 1]);
                args.RemoveAt(index);
                args.RemoveAt(index);
                continue;
            }

            errorMessage = $"Unsupported argument '{args[index]}' for 'serve http'.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static string NormalizeHttpPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultHttpPath;

        string trimmed = value.Trim();
        return trimmed.StartsWith('/')
            ? trimmed
            : "/" + trimmed;
    }
}

public enum CliCommandMode
{
    DirectCli = 0,
    ServeMcp = 1,
    ServeHttp = 2
}
