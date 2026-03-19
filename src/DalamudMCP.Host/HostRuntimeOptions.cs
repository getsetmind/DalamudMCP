namespace DalamudMCP.Host;

public sealed record HostRuntimeOptions(
    HostTransportKind Transport,
    string PipeName,
    int PageSize,
    string HttpHost,
    int HttpPort,
    string McpPath)
{
    public const int DefaultPageSize = 50;
    public const string DefaultHttpHost = "127.0.0.1";
    public const int DefaultHttpPort = 38473;
    public const string DefaultMcpPath = "/mcp";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out HostRuntimeOptions? options,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = null;
        errorMessage = null;

        var transport = HostTransportKind.Stdio;
        string? pipeName = null;
        var pageSize = DefaultPageSize;
        string httpHost = DefaultHttpHost;
        var httpPort = DefaultHttpPort;
        var mcpPath = DefaultMcpPath;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--pipe-name":
                    if (!TryReadValue(args, ref index, out pipeName))
                    {
                        errorMessage = "Missing value for --pipe-name.";
                        return false;
                    }

                    break;

                case "--transport":
                    if (!TryReadValue(args, ref index, out var transportValue))
                    {
                        errorMessage = "Missing value for --transport.";
                        return false;
                    }

                    if (!TryParseTransport(transportValue, out transport))
                    {
                        errorMessage = "--transport must be either 'stdio' or 'http'.";
                        return false;
                    }

                    break;

                case "--page-size":
                    if (!TryReadValue(args, ref index, out var pageSizeValue))
                    {
                        errorMessage = "Missing value for --page-size.";
                        return false;
                    }

                    if (!int.TryParse(pageSizeValue, out pageSize) || pageSize <= 0)
                    {
                        errorMessage = "--page-size must be a positive integer.";
                        return false;
                    }

                    break;

                case "--http-host":
                    if (!TryReadValue(args, ref index, out var httpHostValue))
                    {
                        errorMessage = "Missing value for --http-host.";
                        return false;
                    }

                    httpHost = httpHostValue!;
                    break;

                case "--http-port":
                    if (!TryReadValue(args, ref index, out var httpPortValue))
                    {
                        errorMessage = "Missing value for --http-port.";
                        return false;
                    }

                    if (!int.TryParse(httpPortValue, out httpPort) || httpPort <= 0 || httpPort > 65535)
                    {
                        errorMessage = "--http-port must be an integer between 1 and 65535.";
                        return false;
                    }

                    break;

                case "--mcp-path":
                    if (!TryReadValue(args, ref index, out mcpPath))
                    {
                        errorMessage = "Missing value for --mcp-path.";
                        return false;
                    }

                    break;

                case "--help":
                case "-h":
                    errorMessage = null;
                    return false;

                default:
                    errorMessage = $"Unknown argument '{argument}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(pipeName))
        {
            errorMessage = "The --pipe-name argument is required.";
            return false;
        }

        options = new HostRuntimeOptions(transport, pipeName, pageSize, httpHost, httpPort, NormalizePath(mcpPath));
        return true;
    }

    public static string Usage =>
        "Usage: DalamudMCP.Host --pipe-name <name> [--transport <stdio|http>] [--page-size <positive integer>] [--http-host <hostname>] [--http-port <1-65535>] [--mcp-path </path>]";

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        value = null;
        var nextIndex = index + 1;
        if (nextIndex >= args.Count)
        {
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryParseTransport(string? transportValue, out HostTransportKind transport)
    {
        transport = HostTransportKind.Stdio;
        if (string.Equals(transportValue, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(transportValue, "http", StringComparison.OrdinalIgnoreCase))
        {
            transport = HostTransportKind.Http;
            return true;
        }

        return false;
    }

    private static string NormalizePath(string? path)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return DefaultMcpPath;
        }

        if (trimmed[0] != '/')
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.TrimEnd('/');
    }
}
