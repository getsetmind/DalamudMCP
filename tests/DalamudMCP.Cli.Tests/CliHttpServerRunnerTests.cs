using System.Buffers.Binary;
using System.Globalization;
using System.IO.Pipes;
using System.Net;
using System.Net.Http.Headers;
using DalamudMCP.Protocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DalamudMCP.Cli.Tests;

public sealed class CliHttpServerRunnerTests
{
    private static readonly string[] PlayerContextCliPath = ["player", "context"];
    private static readonly string[][] EmptyCliAliases = [];

    [Theory]
    [InlineData("2025-03-26")]
    [InlineData("2026-07-28")]
    public async Task RunAsync_serves_streamable_http_endpoint_for_supported_protocol_versions(string protocolVersion)
    {
        string pipeName = $"DalamudMCP.Test.{Guid.NewGuid():N}";
        int port = GetFreePort();
        bool parsed = CliRuntimeOptions.TryParse(
            ["--pipe", pipeName, "serve", "http", "--port", port.ToString(CultureInfo.InvariantCulture)],
            out CliRuntimeOptions? options,
            out string? errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);

        const string bearerToken = "integration-test-token";
        Environment.SetEnvironmentVariable(ProtocolContract.HttpBearerTokenEnvironmentVariableName, bearerToken);
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(10));
        Task describeServerTask = RunDescribeOperationsServerAsync(pipeName, cancellationTokenSource.Token);
        Task<int> runnerTask = CliHttpServerRunner.RunAsync(options, cancellationTokenSource.Token);

        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        Uri endpoint = new($"http://127.0.0.1:{port}/mcp");
        await WaitForServerAsync(client, endpoint, cancellationTokenSource.Token);

        using HttpResponseMessage unauthorizedResponse = await client.GetAsync(endpoint, cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using HttpResponseMessage healthResponse = await client.GetAsync(endpoint, cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, healthResponse.StatusCode);

        using HttpRequestMessage rejectedOriginRequest = new(HttpMethod.Get, endpoint);
        rejectedOriginRequest.Headers.Add("Origin", "https://example.com");
        using HttpResponseMessage rejectedOriginResponse = await client.SendAsync(
            rejectedOriginRequest,
            cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.Forbidden, rejectedOriginResponse.StatusCode);

        await using HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {bearerToken}"
            }
        });
        await using McpClient mcpClient = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ProtocolVersion = protocolVersion,
                ClientInfo = new Implementation
                {
                    Name = "DalamudMCP.Cli.Tests",
                    Version = "1.0.0"
                },
                Capabilities = new ClientCapabilities()
            },
            cancellationToken: cancellationTokenSource.Token);
        var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationTokenSource.Token);
        Assert.Contains(tools, static tool => string.Equals(tool.Name, "get_player_context", StringComparison.Ordinal));

        cancellationTokenSource.Cancel();
        await describeServerTask;
        int exitCode = await runnerTask;
        Assert.Equal(0, exitCode);
        Environment.SetEnvironmentVariable(ProtocolContract.HttpBearerTokenEnvironmentVariableName, null);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://127.0.0.1:8443", true)]
    [InlineData("https://example.com", false)]
    [InlineData("null", false)]
    [InlineData("not a uri", false)]
    public void IsAllowedOrigin_only_accepts_loopback_origins(string? origin, bool expected)
    {
        Assert.Equal(expected, CliHttpServerRunner.IsAllowedOrigin(origin));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, "secret", false)]
    [InlineData("Basic secret", "secret", false)]
    [InlineData("Bearer wrong", "secret", false)]
    [InlineData("Bearer secret", "secret", true)]
    public void IsAuthorized_validates_configured_bearer_token(
        string? authorizationHeader,
        string? configuredToken,
        bool expected)
    {
        Assert.Equal(expected, CliHttpServerRunner.IsAuthorized(authorizationHeader, configuredToken));
    }

    private static async Task WaitForServerAsync(HttpClient client, Uri endpoint, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(endpoint, cancellationToken);
                if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Unauthorized)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
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
