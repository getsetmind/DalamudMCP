using System.Net;
using System.Net.Http.Headers;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class StreamableHttpTransportHostTests
{
    [Fact]
    public async Task RunAsync_ProcessesInitializeAndToolCallsOverHttp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HttpTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default
                .EnableTool("get_player_context")
                .EnableTool("get_session_status")
                .EnableResource("ffxiv://player/context")
                .EnableResource("ffxiv://session/status"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var port = GetFreePort();
        var transport = new StreamableHttpTransportHost(
            StdioTransportHost.CreateForPipe(root.Options.PipeName),
            "127.0.0.1",
            port,
            "/mcp");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runTask = transport.RunAsync(cts.Token);
        try
        {
            using var httpClient = new HttpClient();
            var initializeResponse = await PostAsync(
                httpClient,
                $"http://127.0.0.1:{port}/mcp",
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"host-tests","version":"1.0.0"}}}""",
                includeOrigin: false,
                cancellationToken);

            var toolResponse = await PostAsync(
                httpClient,
                $"http://127.0.0.1:{port}/mcp",
                """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_player_context","arguments":{}}}""",
                includeOrigin: false,
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
            Assert.Equal("application/json", initializeResponse.Content.Headers.ContentType?.MediaType);
            Assert.Contains("\"protocolVersion\":\"2025-11-25\"", await initializeResponse.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);

            Assert.Equal(HttpStatusCode.OK, toolResponse.StatusCode);
            var toolResponseBody = await toolResponse.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(
                toolResponseBody.Contains("characterName", StringComparison.Ordinal)
                || toolResponseBody.Contains("player_not_ready", StringComparison.Ordinal),
                $"Unexpected tool response: {toolResponseBody}");
        }
        finally
        {
            cts.Cancel();
            await runTask;
            await transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsForbidden_WhenOriginIsNotLocalhost()
    {
        var transport = new StreamableHttpTransportHost(
            new StdioTransportHost(
                new McpServerHost((_, _) => throw new NotSupportedException()),
                (_, _, _) => Task.FromResult<object>(new { }),
                (_, _) => Task.FromResult<object>(new { })),
            "127.0.0.1",
            GetFreePort(),
            "/mcp");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = transport.RunAsync(cts.Token);
        try
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, transport.EndpointUrl)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}"""),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Origin", "https://example.com");

            var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            cts.Cancel();
            await runTask;
            await transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsMethodNotAllowed_ForGetRequests()
    {
        var transport = new StreamableHttpTransportHost(
            new StdioTransportHost(
                new McpServerHost((_, _) => throw new NotSupportedException()),
                (_, _, _) => Task.FromResult<object>(new { }),
                (_, _) => Task.FromResult<object>(new { })),
            "127.0.0.1",
            GetFreePort(),
            "/mcp");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = transport.RunAsync(cts.Token);
        try
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, transport.EndpointUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
        finally
        {
            cts.Cancel();
            await runTask;
            await transport.DisposeAsync();
        }
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient httpClient,
        string url,
        string json,
        bool includeOrigin,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (includeOrigin)
        {
            request.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1");
        }

        return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
