using System.Runtime.CompilerServices;
using System.Text.Json;
using DalamudMCP.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DalamudMCP.Cli.Tests;

public sealed class RemoteMcpToolServiceTests
{
    [Fact]
    public async Task ListToolsAsync_does_not_allocate_after_warmup()
    {
        RemoteMcpToolService service = new(
            [
                new ProtocolOperationDescriptor(
                    "player.context",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current player context.",
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context")
            ],
            new FakeProtocolOperationClient());
        RequestContext<ListToolsRequestParams> requestContext = CreateRequestContext<ListToolsRequestParams>();

        _ = await service.ListToolsAsync(requestContext, TestContext.Current.CancellationToken);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ListToolsResult result = await service.ListToolsAsync(requestContext, TestContext.Current.CancellationToken);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Single(result.Tools);
        Assert.Equal(0, after - before);
    }

    [Fact]
    public async Task ListToolsAsync_refreshes_catalog_when_interval_expires()
    {
        FakeProtocolOperationClient client = new(
            new DescribeOperationsResponse(
            [
                new ProtocolOperationDescriptor(
                    "player.context",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current player context.",
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context"),
                new ProtocolOperationDescriptor(
                    "inventory.summary",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current inventory summary.",
                    Summary: "Gets inventory summary.",
                    CliCommandPath: ["inventory", "summary"],
                    McpToolName: "get_inventory_summary")
            ]));

        RemoteMcpToolService service = new(
            [
                new ProtocolOperationDescriptor(
                    "player.context",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current player context.",
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context")
            ],
            client,
            TimeSpan.FromMilliseconds(1));

        RequestContext<ListToolsRequestParams> requestContext = CreateRequestContext<ListToolsRequestParams>();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        ListToolsResult result = await service.ListToolsAsync(requestContext, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Tools.Count);
        Assert.Contains(result.Tools, static tool => string.Equals(tool.Name, "get_inventory_summary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListToolsAsync_maps_operation_contract_to_mcp_metadata()
    {
        const string outputSchema = "{\"type\":\"object\",\"properties\":{\"success\":{\"type\":\"boolean\"}}}";
        RemoteMcpToolService service = new(
            [
                new ProtocolOperationDescriptor(
                    "plugin.package.control",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Controls packages.",
                    Summary: "Controls plugin packages.",
                    McpToolName: "plugin_package",
                    Effect: ProtocolOperationEffect.Destructive,
                    PermissionScope: "plugin.package",
                    Idempotent: false,
                    OpenWorld: true,
                    SupportsDryRun: true,
                    OutputSchemaJson: outputSchema)
            ],
            new FakeProtocolOperationClient());

        ListToolsResult result = await service.ListToolsAsync(
            CreateRequestContext<ListToolsRequestParams>(),
            TestContext.Current.CancellationToken);

        Tool tool = Assert.Single(result.Tools);
        Assert.Equal("Controls plugin packages.", tool.Title);
        Assert.True(tool.Annotations?.DestructiveHint);
        Assert.False(tool.Annotations?.ReadOnlyHint);
        Assert.True(tool.Annotations?.OpenWorldHint);
        Assert.Equal("object", tool.OutputSchema?.GetProperty("type").GetString());
        Assert.Equal("plugin.package", tool.Meta?["io.dalamudmcp/permissionScope"]?.GetValue<string>());
        Assert.True(tool.Meta?["io.dalamudmcp/supportsDryRun"]?.GetValue<bool>());
    }

    [Fact]
    public async Task ListToolsAsync_emits_parameter_bounds_and_enums()
    {
        RemoteMcpToolService service = new(
            [
                new ProtocolOperationDescriptor(
                    "game-data.search",
                    ProtocolOperationVisibility.Both,
                    [
                        new ProtocolParameterDescriptor(
                            "Limit",
                            ProtocolValueKind.Number,
                            ProtocolParameterSource.Option,
                            false,
                            McpName: "limit",
                            Minimum: 1,
                            Maximum: 100),
                        new ProtocolParameterDescriptor(
                            "Language",
                            ProtocolValueKind.Text,
                            ProtocolParameterSource.Option,
                            false,
                            McpName: "language",
                            MaxLength: 16,
                            AllowedValues: ["en", "ja"])
                    ],
                    McpToolName: "game_data_search")
            ],
            new FakeProtocolOperationClient());

        ListToolsResult result = await service.ListToolsAsync(
            CreateRequestContext<ListToolsRequestParams>(),
            TestContext.Current.CancellationToken);
        Tool tool = Assert.Single(result.Tools);
        JsonElement properties = tool.InputSchema.GetProperty("properties");

        Assert.Equal(1, properties.GetProperty("limit").GetProperty("minimum").GetDouble());
        Assert.Equal(100, properties.GetProperty("limit").GetProperty("maximum").GetDouble());
        Assert.Equal(16, properties.GetProperty("language").GetProperty("maxLength").GetInt32());
        Assert.Equal(2, properties.GetProperty("language").GetProperty("enum").GetArrayLength());
    }

    [Fact]
    public void TryCreateImageContent_converts_base64_payload_and_sanitizes_structured_data()
    {
        using JsonDocument document = JsonDocument.Parse(
            "{\"mimeType\":\"image/png\",\"base64Data\":\"iVBORw0KGgo=\",\"width\":1}");

        Assert.True(RemoteMcpToolService.TryCreateImageContent(
            document.RootElement,
            out ImageContentBlock image,
            out JsonElement sanitized));

        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(8, image.DecodedData.Length);
        Assert.Equal(JsonValueKind.Null, sanitized.GetProperty("base64Data").ValueKind);
        Assert.Equal(1, sanitized.GetProperty("width").GetInt32());
    }

    [Theory]
    [InlineData("{\"success\":false}", true)]
    [InlineData("{\"succeeded\":false}", true)]
    [InlineData("{\"success\":true}", false)]
    [InlineData("{\"value\":false}", false)]
    public void IsDomainError_recognizes_domain_failure_contract(string json, bool expected)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(expected, RemoteMcpToolService.IsDomainError(document.RootElement));
    }

    private static RequestContext<TParams> CreateRequestContext<TParams>()
        where TParams : class
    {
        return (RequestContext<TParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<TParams>));
    }

    private sealed class FakeProtocolOperationClient : IProtocolOperationClient
    {
        private readonly DescribeOperationsResponse response;

        public FakeProtocolOperationClient(DescribeOperationsResponse? response = null)
        {
            this.response = response ?? new DescribeOperationsResponse([]);
        }

        public ValueTask<ProtocolInvocationResult> InvokeAsync(
            string requestType,
            ProtocolRequestPayload request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> InvokeAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken)
            where TRequest : class
        {
            throw new NotSupportedException();
        }

        public ValueTask<DescribeOperationsResponse> DescribeOperationsAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(response);
        }
    }
}
