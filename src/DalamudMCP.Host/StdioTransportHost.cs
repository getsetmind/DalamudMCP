using System.Text.Json;
using System.Text.Json.Nodes;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Host;

public sealed class StdioTransportHost
{
    private readonly McpServerHost serverHost;
    private readonly Func<string, object?, CancellationToken, Task<object>> toolCallHandler;
    private readonly Func<string, CancellationToken, Task<object>> resourceReadHandler;
    private readonly int defaultPageSize;
    private bool initialized;

    public StdioTransportHost(
        McpServerHost serverHost,
        McpToolInvoker toolInvoker,
        McpResourceReader resourceReader,
        int defaultPageSize = 50)
        : this(
            serverHost,
            (toolName, arguments, cancellationToken) => toolInvoker.InvokeAsync(toolName, arguments, cancellationToken),
            (uri, cancellationToken) => resourceReader.ReadAsync(uri, cancellationToken),
            defaultPageSize)
    {
        ArgumentNullException.ThrowIfNull(toolInvoker);
        ArgumentNullException.ThrowIfNull(resourceReader);
    }

    public StdioTransportHost(
        McpServerHost serverHost,
        Func<string, object?, CancellationToken, Task<object>> toolCallHandler,
        Func<string, CancellationToken, Task<object>> resourceReadHandler,
        int defaultPageSize = 50)
    {
        ArgumentNullException.ThrowIfNull(serverHost);
        ArgumentNullException.ThrowIfNull(toolCallHandler);
        ArgumentNullException.ThrowIfNull(resourceReadHandler);

        if (defaultPageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPageSize), "Page size must be positive.");
        }

        this.serverHost = serverHost;
        this.toolCallHandler = toolCallHandler;
        this.resourceReadHandler = resourceReadHandler;
        this.defaultPageSize = defaultPageSize;
    }

    public static StdioTransportHost CreateForPipe(string pipeName, int defaultPageSize = 50)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var bridgeClient = new PluginBridgeClient(pipeName);
        var capabilityRegistry = Domain.Registry.KnownCapabilityRegistry.CreateDefault();
        var toolRegistry = new McpToolRegistry(capabilityRegistry);
        var resourceRegistry = new McpResourceRegistry(capabilityRegistry);
        var serverHost = new McpServerHost(
            bridgeClient.SendAsync,
            bridgeClient.GetCapabilityStateAsync,
            toolRegistry,
            resourceRegistry,
            new McpSchemaRegistry(),
            new McpRegistryConsistencyValidator(),
            CreateDefaultServerInfo());

        return new StdioTransportHost(
            serverHost,
            new McpToolInvoker(bridgeClient, toolRegistry),
            new McpResourceReader(bridgeClient, resourceRegistry),
            defaultPageSize);
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = await ProcessMessageAsync(line, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                continue;
            }

            await output.WriteAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.WriteAsync("\n".AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string?> ProcessMessageAsync(string requestJson, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(requestJson);
        }
        catch (JsonException)
        {
            return CreateErrorResponse(null, JsonRpcErrorCodes.ParseError, "Invalid JSON-RPC payload.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return CreateErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "JSON-RPC payload must be an object.");
            }

            if (!TryGetString(root, "jsonrpc", out var jsonRpcVersion) || !string.Equals(jsonRpcVersion, "2.0", StringComparison.Ordinal))
            {
                return CreateErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "Only JSON-RPC 2.0 is supported.");
            }

            if (!TryGetString(root, "method", out var method))
            {
                return CreateErrorResponse(CloneIdOrNull(root), JsonRpcErrorCodes.InvalidRequest, "Method is required.");
            }

            var methodName = method!;

            var id = CloneIdOrNull(root);
            var hasId = id.HasValue && id.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            root.TryGetProperty("params", out var parameters);

            if (!hasId && methodName.StartsWith("notifications/", StringComparison.Ordinal))
            {
                await HandleNotificationAsync(methodName, parameters, cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (!hasId)
            {
                return null;
            }

            if (!initialized && !string.Equals(methodName, "initialize", StringComparison.Ordinal) && !string.Equals(methodName, "ping", StringComparison.Ordinal))
            {
                return CreateErrorResponse(id, JsonRpcErrorCodes.ServerNotInitialized, "Server has not been initialized.");
            }

            try
            {
                return methodName switch
                {
                    "initialize" => CreateSuccessResponse(id, await HandleInitializeAsync(parameters, cancellationToken).ConfigureAwait(false)),
                    "ping" => CreateSuccessResponse(id, new JsonRpcEmptyResult()),
                    "tools/list" => CreateSuccessResponse(id, await HandleListToolsAsync(parameters, cancellationToken).ConfigureAwait(false)),
                    "resources/list" => CreateSuccessResponse(id, await HandleListResourcesAsync(parameters, cancellationToken).ConfigureAwait(false)),
                    "resources/templates/list" => CreateSuccessResponse(id, await HandleListResourceTemplatesAsync(parameters, cancellationToken).ConfigureAwait(false)),
                    "tools/call" => CreateSuccessResponse(id, await HandleCallToolAsync(parameters, cancellationToken).ConfigureAwait(false)),
                    "resources/read" => CreateSuccessResponse(id, await HandleReadResourceAsync(parameters, cancellationToken).ConfigureAwait(false)),
                    _ => CreateErrorResponse(id, JsonRpcErrorCodes.MethodNotFound, $"Method '{methodName}' is not supported."),
                };
            }
            catch (ArgumentException exception)
            {
                return CreateErrorResponse(id, JsonRpcErrorCodes.InvalidParams, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return CreateErrorResponse(id, JsonRpcErrorCodes.InvalidParams, exception.Message);
            }
        }
    }

    private static McpServerInfo CreateDefaultServerInfo() =>
        new(
            Name: "dalamudmcp-host",
            Version: typeof(StdioTransportHost).Assembly.GetName().Version?.ToString() ?? "0.1.0",
            Title: "DalamudMCP Host",
            Description: "MCP host surface for FFXIV observation capabilities bridged from the Dalamud plugin.",
            WebsiteUrl: null);

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static JsonElement? CloneIdOrNull(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id))
        {
            return null;
        }

        return id.Clone();
    }

    private static JsonNode? ToNode(object? value) =>
        value is null ? null : JsonSerializer.SerializeToNode(value, BridgeJson.Options);

    private static string CreateSuccessResponse(JsonElement? id, object result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.HasValue ? JsonNode.Parse(id.Value.GetRawText()) : null,
            ["result"] = ToNode(result),
        };

        return response.ToJsonString(BridgeJson.Options);
    }

    private static string CreateErrorResponse(JsonElement? id, int code, string message)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.HasValue ? JsonNode.Parse(id.Value.GetRawText()) : null,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };

        return response.ToJsonString(BridgeJson.Options);
    }

    private Task HandleNotificationAsync(string method, JsonElement parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = parameters;

        if (string.Equals(method, "notifications/initialized", StringComparison.Ordinal))
        {
            initialized = true;
            return Task.CompletedTask;
        }

        if (method.StartsWith("notifications/", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        throw new InvalidOperationException($"Notification '{method}' is not supported.");
    }

    private async Task<McpInitializeResult> HandleInitializeAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        if (parameters.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("Initialize parameters are required.", nameof(parameters));
        }

        if (!TryGetString(parameters, "protocolVersion", out var protocolVersion))
        {
            throw new ArgumentException("protocolVersion is required.", nameof(parameters));
        }

        if (!parameters.TryGetProperty("clientInfo", out var clientInfoElement) || clientInfoElement.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("clientInfo is required.", nameof(parameters));
        }

        if (!TryGetString(clientInfoElement, "name", out var clientName))
        {
            throw new ArgumentException("clientInfo.name is required.", nameof(parameters));
        }

        if (!TryGetString(clientInfoElement, "version", out var clientVersion))
        {
            throw new ArgumentException("clientInfo.version is required.", nameof(parameters));
        }

        string? clientTitle = null;
        _ = TryGetString(clientInfoElement, "title", out clientTitle);

        var request = new McpInitializeRequest(
            protocolVersion!,
            ReadClientCapabilities(parameters),
            new McpClientInfo(clientName!, clientVersion!, clientTitle));

        var result = await serverHost.InitializeAsync(request, cancellationToken).ConfigureAwait(false);
        initialized = true;
        return result;
    }

    private async Task<McpListToolsResult> HandleListToolsAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var cursor = ReadCursor(parameters);
        return await serverHost.ListToolsAsync(cursor, defaultPageSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<McpListResourcesResult> HandleListResourcesAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var cursor = ReadCursor(parameters);
        return await serverHost.ListResourcesAsync(cursor, defaultPageSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<McpListResourceTemplatesResult> HandleListResourceTemplatesAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var cursor = ReadCursor(parameters);
        return await serverHost.ListResourceTemplatesAsync(cursor, defaultPageSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<McpCallToolResult> HandleCallToolAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        if (parameters.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("tools/call parameters are required.", nameof(parameters));
        }

        if (!TryGetString(parameters, "name", out var toolName))
        {
            throw new ArgumentException("tools/call name is required.", nameof(parameters));
        }

        object? arguments = parameters.TryGetProperty("arguments", out var argumentsElement)
            ? argumentsElement.Clone()
            : null;
        var result = await toolCallHandler(toolName!, arguments, cancellationToken).ConfigureAwait(false);
        var serializedResult = BridgeJson.Serialize(result);

        return new McpCallToolResult(
            [new McpTextContent("text", serializedResult)],
            result,
            IsError: false);
    }

    private async Task<McpReadResourceResult> HandleReadResourceAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        if (parameters.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("resources/read parameters are required.", nameof(parameters));
        }

        if (!TryGetString(parameters, "uri", out var uri))
        {
            throw new ArgumentException("resources/read uri is required.", nameof(parameters));
        }

        var result = await resourceReadHandler(uri!, cancellationToken).ConfigureAwait(false);

        return new McpReadResourceResult(
            [new McpTextResourceContents(uri!, McpContentTypes.ApplicationJson, BridgeJson.Serialize(result))]);
    }

    private static string? ReadCursor(JsonElement parameters)
    {
        if (parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (parameters.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("Pagination parameters must be an object.", nameof(parameters));
        }

        return TryGetString(parameters, "cursor", out var cursor) ? cursor : null;
    }

    private static McpClientCapabilities ReadClientCapabilities(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind is not JsonValueKind.Object)
        {
            return new McpClientCapabilities(false, false, false);
        }

        var toolsListChanged = capabilities.TryGetProperty("tools", out var tools)
            && tools.ValueKind is JsonValueKind.Object
            && tools.TryGetProperty("listChanged", out var toolsListChangedElement)
            && toolsListChangedElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            && toolsListChangedElement.GetBoolean();

        var resourcesSubscribe = capabilities.TryGetProperty("resources", out var resources)
            && resources.ValueKind is JsonValueKind.Object
            && resources.TryGetProperty("subscribe", out var resourcesSubscribeElement)
            && resourcesSubscribeElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            && resourcesSubscribeElement.GetBoolean();

        var resourcesListChanged = capabilities.TryGetProperty("resources", out resources)
            && resources.ValueKind is JsonValueKind.Object
            && resources.TryGetProperty("listChanged", out var resourcesListChangedElement)
            && resourcesListChangedElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            && resourcesListChangedElement.GetBoolean();

        return new McpClientCapabilities(toolsListChanged, resourcesSubscribe, resourcesListChanged);
    }

    private static class JsonRpcErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int ServerNotInitialized = -32002;
    }

    private sealed record JsonRpcEmptyResult;

    private sealed record McpTextContent(string Type, string Text);

    private sealed record McpCallToolResult(
        IReadOnlyList<McpTextContent> Content,
        object StructuredContent,
        bool IsError);

    private sealed record McpTextResourceContents(
        string Uri,
        string? MimeType,
        string Text);

    private sealed record McpReadResourceResult(
        IReadOnlyList<McpTextResourceContents> Contents);
}
