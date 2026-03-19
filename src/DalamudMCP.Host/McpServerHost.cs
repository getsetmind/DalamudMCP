using System.Text.Json;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Host;

public sealed class McpServerHost
{
    private readonly Func<BridgeRequestEnvelope, CancellationToken, Task<BridgeResponseEnvelope>> requestHandler;
    private readonly Func<CancellationToken, Task<CapabilityStateResponse>>? capabilityStateProvider;
    private readonly McpToolRegistry toolRegistry;
    private readonly McpResourceRegistry resourceRegistry;
    private readonly McpSchemaRegistry schemaRegistry;
    private readonly McpRegistryConsistencyValidator registryConsistencyValidator;
    private readonly McpServerInfo serverInfo;

    public McpServerHost(BridgeRequestDispatcher dispatcher)
        : this(dispatcher.DispatchAsync)
    {
    }

    public McpServerHost(Func<BridgeRequestEnvelope, CancellationToken, Task<BridgeResponseEnvelope>> requestHandler)
        : this(
            requestHandler,
            capabilityStateProvider: null,
            McpToolRegistry.CreateDefault(),
            McpResourceRegistry.CreateDefault(),
            new McpSchemaRegistry(),
            new McpRegistryConsistencyValidator(),
            CreateDefaultServerInfo())
    {
    }

    public McpServerHost(
        Func<BridgeRequestEnvelope, CancellationToken, Task<BridgeResponseEnvelope>> requestHandler,
        Func<CancellationToken, Task<CapabilityStateResponse>>? capabilityStateProvider,
        McpToolRegistry toolRegistry,
        McpResourceRegistry resourceRegistry,
        McpSchemaRegistry schemaRegistry,
        McpRegistryConsistencyValidator registryConsistencyValidator,
        McpServerInfo serverInfo)
    {
        ArgumentNullException.ThrowIfNull(requestHandler);
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(resourceRegistry);
        ArgumentNullException.ThrowIfNull(schemaRegistry);
        ArgumentNullException.ThrowIfNull(registryConsistencyValidator);
        ArgumentNullException.ThrowIfNull(serverInfo);
        this.requestHandler = requestHandler;
        this.capabilityStateProvider = capabilityStateProvider;
        this.toolRegistry = toolRegistry;
        this.resourceRegistry = resourceRegistry;
        this.schemaRegistry = schemaRegistry;
        this.registryConsistencyValidator = registryConsistencyValidator;
        this.serverInfo = serverInfo;
        this.registryConsistencyValidator.Validate(toolRegistry, resourceRegistry, schemaRegistry);
    }

    public static McpServerHost CreateForPipe(string pipeName)
    {
        var client = new PluginBridgeClient(pipeName);
        return new McpServerHost(
            client.SendAsync,
            client.GetCapabilityStateAsync,
            McpToolRegistry.CreateDefault(),
            McpResourceRegistry.CreateDefault(),
            new McpSchemaRegistry(),
            new McpRegistryConsistencyValidator(),
            CreateDefaultServerInfo());
    }

    public Task<McpInitializeResult> InitializeAsync(McpInitializeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!McpProtocolVersion.IsSupported(request.ProtocolVersion))
        {
            throw new InvalidOperationException($"Unsupported MCP protocol version '{request.ProtocolVersion}'.");
        }

        var result = new McpInitializeResult(
            McpProtocolVersion.Current,
            new McpServerCapabilities(
                new McpToolsCapability(ListChanged: false),
                new McpResourcesCapability(Subscribe: false, ListChanged: false)),
            serverInfo,
            "DalamudMCP exposes FFXIV observation primitives. Action capabilities remain private experimental.");

        return Task.FromResult(result);
    }

    public async Task<McpListToolsResult> ListToolsAsync(string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (offset, take) = McpPagination.Parse(cursor, pageSize);
        var enabledTools = await GetEnabledToolsAsync(cancellationToken).ConfigureAwait(false);
        var visibleTools = toolRegistry.Tools
            .Where(tool => enabledTools is null || enabledTools.Contains(tool.ToolName))
            .ToArray();
        var page = visibleTools
            .Skip(offset)
            .Take(take)
            .Select(tool => new McpListedTool(
                tool.ToolName,
                tool.DisplayName,
                tool.Description,
                schemaRegistry.GetRequired(tool.InputSchemaId),
                schemaRegistry.GetRequired(tool.OutputSchemaId),
                new McpToolAnnotations(
                    Title: tool.DisplayName,
                    ReadOnlyHint: tool.Profile is not Domain.Capabilities.ProfileType.Action,
                    DestructiveHint: tool.Profile is Domain.Capabilities.ProfileType.Action,
                    IdempotentHint: tool.Profile is not Domain.Capabilities.ProfileType.Action,
                    OpenWorldHint: tool.Profile is Domain.Capabilities.ProfileType.Action)))
            .ToArray();

        return new McpListToolsResult(page, McpPagination.CreateNextCursor(offset, take, visibleTools.Length));
    }

    public async Task<McpListResourcesResult> ListResourcesAsync(string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (offset, take) = McpPagination.Parse(cursor, pageSize);
        var enabledResources = await GetEnabledResourcesAsync(cancellationToken).ConfigureAwait(false);
        var resources = resourceRegistry.Resources
            .Where(static resource => !resource.UriTemplate.Contains('{', StringComparison.Ordinal))
            .Where(resource => enabledResources is null || enabledResources.Contains(resource.UriTemplate))
            .ToArray();
        var page = resources
            .Skip(offset)
            .Take(take)
            .Select(resource => new McpListedResource(
                resource.UriTemplate,
                resource.UriTemplate,
                resource.DisplayName,
                resource.Description,
                resource.MimeType))
            .ToArray();

        return new McpListResourcesResult(page, McpPagination.CreateNextCursor(offset, take, resources.Length));
    }

    public async Task<McpListResourceTemplatesResult> ListResourceTemplatesAsync(string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (offset, take) = McpPagination.Parse(cursor, pageSize);
        var enabledResources = await GetEnabledResourcesAsync(cancellationToken).ConfigureAwait(false);
        var resources = resourceRegistry.Resources
            .Where(static resource => resource.UriTemplate.Contains('{', StringComparison.Ordinal))
            .Where(resource => enabledResources is null || enabledResources.Contains(resource.UriTemplate))
            .ToArray();
        var page = resources
            .Skip(offset)
            .Take(take)
            .Select(resource => new McpListedResourceTemplate(
                resource.UriTemplate,
                resource.UriTemplate,
                resource.DisplayName,
                resource.Description,
                resource.MimeType))
            .ToArray();

        return new McpListResourceTemplatesResult(page, McpPagination.CreateNextCursor(offset, take, resources.Length));
    }

    public Task<BridgeResponseEnvelope> HandleAsync(BridgeRequestEnvelope request, CancellationToken cancellationToken) =>
        requestHandler(request, cancellationToken);

    public async Task<string> HandleJsonAsync(string requestJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return BridgeJson.Serialize(CreateInvalidRequestResponse(string.Empty, "Request JSON is required."));
        }

        try
        {
            var request = BridgeJson.Deserialize<BridgeRequestEnvelope>(requestJson);
            if (request is null)
            {
                return BridgeJson.Serialize(CreateInvalidRequestResponse(string.Empty, "Request envelope could not be deserialized."));
            }

            var response = await requestHandler(request, cancellationToken).ConfigureAwait(false);
            return BridgeJson.Serialize(response);
        }
        catch (JsonException)
        {
            return BridgeJson.Serialize(CreateInvalidRequestResponse(string.Empty, "Request JSON is invalid."));
        }
    }

    private static BridgeResponseEnvelope CreateInvalidRequestResponse(string requestId, string errorMessage) =>
        new(
            ContractVersion.Current,
            requestId,
            BridgeResponseTypes.Error,
            Success: false,
            ErrorCode: "invalid_request",
            ErrorMessage: errorMessage,
            Payload: null);

    private static McpServerInfo CreateDefaultServerInfo() =>
        new(
            Name: "dalamudmcp-host",
            Version: typeof(McpServerHost).Assembly.GetName().Version?.ToString() ?? "0.1.0",
            Title: "DalamudMCP Host",
            Description: "MCP host surface for FFXIV observation capabilities bridged from the Dalamud plugin.",
            WebsiteUrl: null);

    private async Task<HashSet<string>?> GetEnabledToolsAsync(CancellationToken cancellationToken)
    {
        if (capabilityStateProvider is null)
        {
            return null;
        }

        var state = await capabilityStateProvider(cancellationToken).ConfigureAwait(false);
        return toolRegistry.Tools
            .Where(tool =>
                state.EnabledTools.Contains(tool.ToolName, StringComparer.OrdinalIgnoreCase)
                && (tool.Profile is Domain.Capabilities.ProfileType.Action
                    ? state.ActionProfileEnabled
                    : state.ObservationProfileEnabled))
            .Select(static tool => tool.ToolName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>?> GetEnabledResourcesAsync(CancellationToken cancellationToken)
    {
        if (capabilityStateProvider is null)
        {
            return null;
        }

        var state = await capabilityStateProvider(cancellationToken).ConfigureAwait(false);
        return state.ObservationProfileEnabled
            ? state.EnabledResources.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
    }
}
