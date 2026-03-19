using System.Net;
using System.Text;

namespace DalamudMCP.Host;

public sealed class StreamableHttpTransportHost : IAsyncDisposable
{
    private readonly StdioTransportHost rpcHost;
    private readonly string httpHost;
    private readonly int port;
    private readonly string mcpPath;
    private readonly HttpListener listener = new();

    public StreamableHttpTransportHost(
        StdioTransportHost rpcHost,
        string httpHost,
        int port,
        string mcpPath)
    {
        ArgumentNullException.ThrowIfNull(rpcHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpPath);

        if (port <= 0 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "HTTP port must be between 1 and 65535.");
        }

        this.rpcHost = rpcHost;
        this.httpHost = httpHost.Trim();
        this.port = port;
        this.mcpPath = NormalizePath(mcpPath);
        listener.Prefixes.Add($"http://{this.httpHost}:{this.port}/");
    }

    public string EndpointUrl => $"http://{httpHost}:{port}{mcpPath}";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        listener.Start();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException) when (!listener.IsListening)
                {
                    break;
                }

                _ = Task.Run(() => HandleContextAsync(context, cancellationToken), CancellationToken.None);
            }
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        listener.Close();
        return ValueTask.CompletedTask;
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsPathMatch(context.Request.Url))
            {
                await WriteStatusAsync(context.Response, HttpStatusCode.NotFound, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!IsOriginAllowed(context.Request.Headers["Origin"]))
            {
                await WriteStatusAsync(context.Response, HttpStatusCode.Forbidden, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!IsProtocolVersionAllowed(context.Request.Headers["MCP-Protocol-Version"]))
            {
                await WriteStatusAsync(context.Response, HttpStatusCode.BadRequest, cancellationToken).ConfigureAwait(false);
                return;
            }

            switch (context.Request.HttpMethod)
            {
                case "POST":
                    await HandlePostAsync(context, cancellationToken).ConfigureAwait(false);
                    return;

                case "GET":
                case "DELETE":
                    await WriteStatusAsync(context.Response, HttpStatusCode.MethodNotAllowed, cancellationToken).ConfigureAwait(false);
                    return;

                default:
                    await WriteStatusAsync(context.Response, HttpStatusCode.MethodNotAllowed, cancellationToken).ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception exception)
        {
            var response = context.Response;
            if (response.OutputStream.CanWrite)
            {
                await WriteJsonAsync(
                        response,
                        HttpStatusCode.InternalServerError,
                        $$"""{"jsonrpc":"2.0","error":{"code":-32603,"message":"{{EscapeJson(exception.Message)}}"},"id":null}""",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task HandlePostAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (!IsAcceptHeaderAllowed(context.Request.AcceptTypes))
        {
            await WriteStatusAsync(context.Response, HttpStatusCode.NotAcceptable, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var requestJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.BadRequest,
                    """{"jsonrpc":"2.0","error":{"code":-32600,"message":"Request body is required."},"id":null}""",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var responseJson = await rpcHost.ProcessMessageAsync(requestJson, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Accepted;
            context.Response.Headers["MCP-Protocol-Version"] = McpProtocolVersion.Current;
            return;
        }

        await WriteJsonAsync(context.Response, HttpStatusCode.OK, responseJson, cancellationToken).ConfigureAwait(false);
    }

    private bool IsPathMatch(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return false;
        }

        var path = requestUri.AbsolutePath.TrimEnd('/');
        var normalizedRequestPath = string.IsNullOrWhiteSpace(path) ? "/" : path;
        return string.Equals(normalizedRequestPath, mcpPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOriginAllowed(string? originHeader)
    {
        if (string.IsNullOrWhiteSpace(originHeader))
        {
            return true;
        }

        if (!Uri.TryCreate(originHeader, UriKind.Absolute, out var origin))
        {
            return false;
        }

        return origin.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocolVersionAllowed(string? protocolVersionHeader)
    {
        if (string.IsNullOrWhiteSpace(protocolVersionHeader))
        {
            return true;
        }

        return McpProtocolVersion.IsSupported(protocolVersionHeader);
    }

    private static bool IsAcceptHeaderAllowed(string[]? acceptTypes)
    {
        if (acceptTypes is null || acceptTypes.Length == 0)
        {
            return true;
        }

        return acceptTypes.Any(static value =>
            value.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || value.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
            || value.Contains("*/*", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteStatusAsync(HttpListenerResponse response, HttpStatusCode statusCode, CancellationToken cancellationToken)
    {
        response.StatusCode = (int)statusCode;
        response.Headers["MCP-Protocol-Version"] = McpProtocolVersion.Current;
        await response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode statusCode, string json, CancellationToken cancellationToken)
    {
        var buffer = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = buffer.LongLength;
        response.Headers["MCP-Protocol-Version"] = McpProtocolVersion.Current;
        await response.OutputStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            return "/mcp";
        }

        if (trimmed[0] != '/')
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.TrimEnd('/');
    }

    private static string EscapeJson(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
