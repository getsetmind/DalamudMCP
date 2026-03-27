using System.Buffers.Binary;
using System.Globalization;
using System.IO.Pipes;
using System.Net;
using System.Net.Http.Json;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli.Tests;

public sealed class CliHttpServerRunnerTests
{
    private static readonly string[] PlayerContextCliPath = ["player", "context"];
    private static readonly string[][] EmptyCliAliases = [];

    [Fact]
    public async Task RunAsync_serves_streamable_http_endpoint()
    {
        string pipeName = $"DalamudMCP.Test.{Guid.NewGuid():N}";
        int port = GetFreePort();
        bool parsed = CliRuntimeOptions.TryParse(
            ["--pipe", pipeName, "serve", "http", "--port", port.ToString(CultureInfo.InvariantCulture)],
            out CliRuntimeOptions? options,
            out string? errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);

        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(10));
        Task describeServerTask = RunDescribeOperationsServerAsync(pipeName, cancellationTokenSource.Token);
        Task<int> runnerTask = CliHttpServerRunner.RunAsync(options, cancellationTokenSource.Token);

        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        Uri endpoint = new($"http://127.0.0.1:{port}/mcp");
        await WaitForServerAsync(client, endpoint, cancellationTokenSource.Token);

        using HttpResponseMessage healthResponse = await client.GetAsync(endpoint, cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, healthResponse.StatusCode);
        Assert.True(healthResponse.Headers.Contains("MCP-Protocol-Version"));
        Assert.Contains("2025-03-26", healthResponse.Headers.GetValues("MCP-Protocol-Version"));

        using HttpResponseMessage initializeResponse = await client.PostAsJsonAsync(
            endpoint,
            new
            {
                jsonrpc = "2.0",
                id = "init-1",
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "DalamudMCP.Cli.Tests",
                        version = "1.0.0"
                    }
                }
            },
            cancellationTokenSource.Token);
        string initializeJson = await initializeResponse.Content.ReadAsStringAsync(cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.Equal("text/event-stream", initializeResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"protocolVersion\"", initializeJson, StringComparison.Ordinal);
        Assert.Contains("event: message", initializeJson, StringComparison.Ordinal);

        using HttpResponseMessage toolsResponse = await client.PostAsJsonAsync(
            endpoint,
            new
            {
                jsonrpc = "2.0",
                id = "tools-1",
                method = "tools/list",
                @params = new { }
            },
            cancellationTokenSource.Token);
        string toolsJson = await toolsResponse.Content.ReadAsStringAsync(cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        Assert.Equal("text/event-stream", toolsResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"get_player_context\"", toolsJson, StringComparison.Ordinal);

        cancellationTokenSource.Cancel();
        await describeServerTask;
        int exitCode = await runnerTask;
        Assert.Equal(0, exitCode);
    }

    private static async Task WaitForServerAsync(HttpClient client, Uri endpoint, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(endpoint, cancellationToken);
                if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                    return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("The MCP HTTP endpoint did not become available.");
    }

    private static int GetFreePort()
    {
        using System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task RunDescribeOperationsServerAsync(string pipeName, CancellationToken cancellationToken)
    {
        await using NamedPipeServerStream server = new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(cancellationToken);
        ProtocolRequestEnvelope request = await ReadFrameAsync(server, cancellationToken);
        Assert.Equal("__system.describe-operations", request.RequestType);
        Assert.Equal(ProtocolPayloadFormat.MemoryPack, request.PreferredResponseFormat);
        await WriteFrameAsync(server, CreateDescribeOperationsResponse(), cancellationToken);
    }

    private static async Task<ProtocolRequestEnvelope> ReadFrameAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[sizeof(int)];
        await ReadExactAsync(stream, header, cancellationToken);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        byte[] payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken);
        return ProtocolContract.DeserializeRequestEnvelope(payload);
    }

    private static async Task WriteFrameAsync(
        PipeStream stream,
        ProtocolResponseEnvelope response,
        CancellationToken cancellationToken)
    {
        byte[] bytes = ProtocolContract.SerializeEnvelope(response);
        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task ReadExactAsync(PipeStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (bytesRead == 0)
                throw new InvalidOperationException("Pipe closed unexpectedly.");

            offset += bytesRead;
        }
    }

    private static ProtocolResponseEnvelope CreateDescribeOperationsResponse()
    {
        return ProtocolContract.CreateSuccessResponse(
            "req-1",
            new DescribeOperationsResponse(
                [
                    new ProtocolOperationDescriptor(
                        "player.context",
                        ProtocolOperationVisibility.Both,
                        [],
                        "Gets the current player context.",
                        "Gets player context.",
                        PlayerContextCliPath,
                        EmptyCliAliases,
                        "get_player_context",
                        false)
                ]),
            typeof(DescribeOperationsResponse),
            preferredPayloadFormat: ProtocolPayloadFormat.MemoryPack);
    }
}
