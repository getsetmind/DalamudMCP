using System.Text.Json;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class StdioTransportHostTests
{
    [Fact]
    public async Task ProcessMessageAsync_InitializeRequest_ReturnsJsonRpcResponse()
    {
        var host = CreateTransport();

        var responseJson = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{"tools":{"listChanged":false},"resources":{"subscribe":false,"listChanged":false}},"clientInfo":{"name":"codex-cli","version":"1.0.0","title":"Codex CLI"}}}
            """,
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        Assert.Equal("2.0", response.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, response.RootElement.GetProperty("id").GetInt32());
        var result = response.RootElement.GetProperty("result");
        Assert.Equal(McpProtocolVersion.Current, result.GetProperty("protocolVersion").GetString());
        Assert.Equal("dalamudmcp-host", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ProcessMessageAsync_ToolsListBeforeInitialize_ReturnsError()
    {
        var host = CreateTransport();

        var responseJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"req-1","method":"tools/list"}""",
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var error = response.RootElement.GetProperty("error");
        Assert.Equal(-32002, error.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ProcessMessageAsync_ToolsListAfterInitialize_ReturnsRegistryPage()
    {
        var host = CreateTransport(defaultPageSize: 2);
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"req-2","method":"tools/list"}""",
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var result = response.RootElement.GetProperty("result");
        var tools = result.GetProperty("tools");
        Assert.Equal(2, tools.GetArrayLength());
        Assert.Equal("2", result.GetProperty("nextCursor").GetString());
    }

    [Fact]
    public async Task ProcessMessageAsync_ToolCall_ReturnsTextAndStructuredContent()
    {
        var host = CreateTransport(
            toolResultFactory: static (toolName, arguments) =>
            {
                Assert.Equal("get_addon_tree", toolName);
                Assert.True(arguments.HasValue);
                return new
                {
                    available = true,
                    data = new
                    {
                        addonName = "Inventory",
                    },
                };
            });
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":"req-3","method":"tools/call","params":{"name":"get_addon_tree","arguments":{"addonName":"Inventory"}}}
            """,
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var result = response.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        var content = result.GetProperty("content");
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("Inventory", result.GetProperty("structuredContent").GetProperty("data").GetProperty("addonName").GetString());
    }

    [Fact]
    public async Task ProcessMessageAsync_SessionStatusToolCall_ReturnsStructuredContent()
    {
        var host = CreateTransport(
            toolResultFactory: static (toolName, arguments) =>
            {
                Assert.Equal("get_session_status", toolName);
                Assert.False(arguments.HasValue);
                return new
                {
                    available = true,
                    data = new
                    {
                        pipeName = "DalamudMCP.TestPipe",
                        isBridgeServerRunning = true,
                    },
                };
            });
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":"req-session-tool","method":"tools/call","params":{"name":"get_session_status"}}
            """,
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var structuredContent = response.RootElement.GetProperty("result").GetProperty("structuredContent");
        Assert.True(structuredContent.GetProperty("available").GetBoolean());
        Assert.Equal("DalamudMCP.TestPipe", structuredContent.GetProperty("data").GetProperty("pipeName").GetString());
        Assert.True(structuredContent.GetProperty("data").GetProperty("isBridgeServerRunning").GetBoolean());
    }

    [Fact]
    public async Task ProcessMessageAsync_ReadResource_ReturnsJsonTextResource()
    {
        var host = CreateTransport(
            resourceResultFactory: static uri =>
            {
                Assert.Equal("ffxiv://player/context", uri);
                return new
                {
                    available = true,
                    data = new
                    {
                        characterName = "Test Character",
                    },
                };
            });
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":"req-4","method":"resources/read","params":{"uri":"ffxiv://player/context"}}
            """,
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var contents = response.RootElement.GetProperty("result").GetProperty("contents");
        Assert.Equal(1, contents.GetArrayLength());
        Assert.Equal("ffxiv://player/context", contents[0].GetProperty("uri").GetString());
        Assert.Equal("application/json", contents[0].GetProperty("mimeType").GetString());
        Assert.Contains("Test Character", contents[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReadTemplatedResource_ReturnsJsonTextResource()
    {
        var host = CreateTransport(
            resourceResultFactory: static uri =>
            {
                Assert.Equal("ffxiv://ui/addon/Inventory/tree", uri);
                return new
                {
                    available = true,
                    data = new
                    {
                        addonName = "Inventory",
                    },
                };
            });
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":"req-4b","method":"resources/read","params":{"uri":"ffxiv://ui/addon/Inventory/tree"}}
            """,
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var contents = response.RootElement.GetProperty("result").GetProperty("contents");
        Assert.Equal(1, contents.GetArrayLength());
        Assert.Equal("ffxiv://ui/addon/Inventory/tree", contents[0].GetProperty("uri").GetString());
        Assert.Equal(McpContentTypes.ApplicationJson, contents[0].GetProperty("mimeType").GetString());
        Assert.Contains("Inventory", contents[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReadSessionStatusResource_ReturnsJsonTextResource()
    {
        var host = CreateTransport(
            resourceResultFactory: static uri =>
            {
                Assert.Equal("ffxiv://session/status", uri);
                return new
                {
                    available = true,
                    data = new
                    {
                        pipeName = "DalamudMCP.TestPipe",
                        isBridgeServerRunning = true,
                    },
                };
            });
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":"req-session-resource","method":"resources/read","params":{"uri":"ffxiv://session/status"}}
            """,
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var contents = response.RootElement.GetProperty("result").GetProperty("contents");
        Assert.Equal(1, contents.GetArrayLength());
        Assert.Equal("ffxiv://session/status", contents[0].GetProperty("uri").GetString());
        Assert.Equal(McpContentTypes.ApplicationJson, contents[0].GetProperty("mimeType").GetString());
        Assert.Contains("DalamudMCP.TestPipe", contents[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WritesOnlyRequestResponses()
    {
        var host = CreateTransport();
        using var input = new StringReader(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"codex-cli","version":"1.0.0"}}}
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            {"jsonrpc":"2.0","id":2,"method":"ping"}
            """);
        using var output = new StringWriter();

        await host.RunAsync(input, output, TestContext.Current.CancellationToken);

        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(2, lines.Length);
        Assert.All(lines, static line => Assert.Contains("\"jsonrpc\":\"2.0\"", line, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateForPipe_UsesPluginSettingsAndBridgeResponses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.StdioTransportTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_player_context"],
                enabledResources: ["ffxiv://player/context"],
                enabledAddons: [],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var host = StdioTransportHost.CreateForPipe(root.Options.PipeName, defaultPageSize: 20);
        await InitializeAsync(host);

        var toolsListJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"list-tools","method":"tools/list"}""",
            cancellationToken);
        using var toolsList = JsonDocument.Parse(toolsListJson!);
        var tools = toolsList.RootElement.GetProperty("result").GetProperty("tools");
        Assert.Equal(1, tools.GetArrayLength());
        Assert.Equal("get_player_context", tools[0].GetProperty("name").GetString());

        var resourcesListJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"list-resources","method":"resources/list"}""",
            cancellationToken);
        using var resourcesList = JsonDocument.Parse(resourcesListJson!);
        var resources = resourcesList.RootElement.GetProperty("result").GetProperty("resources");
        Assert.Equal(1, resources.GetArrayLength());
        Assert.Equal("ffxiv://player/context", resources[0].GetProperty("uri").GetString());

        var toolCallJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"call-tool","method":"tools/call","params":{"name":"get_player_context","arguments":{}}}""",
            cancellationToken);
        using var toolCall = JsonDocument.Parse(toolCallJson!);
        var structuredContent = toolCall.RootElement.GetProperty("result").GetProperty("structuredContent");
        Assert.False(structuredContent.GetProperty("available").GetBoolean());
        Assert.Equal("player_not_ready", structuredContent.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task CreateForPipe_ExposesSessionStatusWhenEnabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.StdioTransportTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_session_status"],
                enabledResources: ["ffxiv://session/status"],
                enabledAddons: [],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var host = StdioTransportHost.CreateForPipe(root.Options.PipeName, defaultPageSize: 20);
        await InitializeAsync(host);

        var toolsListJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"list-tools-session","method":"tools/list"}""",
            cancellationToken);
        using var toolsList = JsonDocument.Parse(toolsListJson!);
        var tools = toolsList.RootElement.GetProperty("result").GetProperty("tools");
        Assert.Single(tools.EnumerateArray());
        Assert.Equal("get_session_status", tools[0].GetProperty("name").GetString());

        var resourcesListJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"list-resources-session","method":"resources/list"}""",
            cancellationToken);
        using var resourcesList = JsonDocument.Parse(resourcesListJson!);
        var resources = resourcesList.RootElement.GetProperty("result").GetProperty("resources");
        Assert.Single(resources.EnumerateArray());
        Assert.Equal("ffxiv://session/status", resources[0].GetProperty("uri").GetString());

        var toolCallJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"call-session","method":"tools/call","params":{"name":"get_session_status"}}""",
            cancellationToken);
        using var toolCall = JsonDocument.Parse(toolCallJson!);
        var structuredContent = toolCall.RootElement.GetProperty("result").GetProperty("structuredContent");
        Assert.True(structuredContent.GetProperty("available").GetBoolean());
        Assert.Equal(root.Options.PipeName, structuredContent.GetProperty("data").GetProperty("pipeName").GetString());
    }

    [Fact]
    public async Task CreateForPipe_ReturnsJsonRpcError_WhenPluginBridgeIsUnavailable()
    {
        var host = StdioTransportHost.CreateForPipe($"DalamudMCP.Missing.{Guid.NewGuid():N}", defaultPageSize: 20);
        await InitializeAsync(host);

        var responseJson = await host.ProcessMessageAsync(
            """{"jsonrpc":"2.0","id":"missing-tools","method":"tools/list"}""",
            TestContext.Current.CancellationToken);

        using var response = JsonDocument.Parse(responseJson!);
        var error = response.RootElement.GetProperty("error");
        Assert.Equal(-32602, error.GetProperty("code").GetInt32());
        Assert.Contains("Plugin bridge is unavailable.", error.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static async Task InitializeAsync(StdioTransportHost host)
    {
        _ = await host.ProcessMessageAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"codex-cli","version":"1.0.0"}}}
            """,
            TestContext.Current.CancellationToken);
    }

    private static StdioTransportHost CreateTransport(
        int defaultPageSize = 50,
        Func<string, JsonElement?, object>? toolResultFactory = null,
        Func<string, object>? resourceResultFactory = null)
    {
        var serverHost = new McpServerHost(static (_, _) => Task.FromResult(
            new DalamudMCP.Contracts.Bridge.BridgeResponseEnvelope(
                DalamudMCP.Contracts.Bridge.ContractVersion.Current,
                "req-stdio",
                DalamudMCP.Infrastructure.Bridge.BridgeResponseTypes.Error,
                Success: false,
                ErrorCode: "not_implemented",
                ErrorMessage: "Not used in StdioTransportHost tests.",
                Payload: null)));

        return new StdioTransportHost(
            serverHost,
            (toolName, arguments, _) =>
            {
                var argumentsElement = arguments is JsonElement element ? element : (JsonElement?)null;
                var result = toolResultFactory?.Invoke(toolName, argumentsElement) ?? new { ok = true, toolName };
                return Task.FromResult((object)result);
            },
            (uri, _) =>
            {
                var result = resourceResultFactory?.Invoke(uri) ?? new { ok = true, uri };
                return Task.FromResult((object)result);
            },
            defaultPageSize);
    }
}
