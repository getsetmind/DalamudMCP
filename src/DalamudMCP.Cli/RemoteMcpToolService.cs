using System.Text.Json;
using System.Text.Json.Nodes;
using DalamudMCP.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DalamudMCP.Cli;

public sealed class RemoteMcpToolService
{
    private readonly object syncRoot = new();
    private readonly IProtocolOperationClient protocolClient;
    private readonly TimeSpan refreshInterval;
    private ProtocolOperationDescriptor[] operations = [];
    private Dictionary<string, ProtocolOperationDescriptor> operationsByToolName = new(StringComparer.Ordinal);
    private ListToolsResult listToolsResult = new() { Tools = [] };
    private long nextRefreshAt;
    private Task? refreshTask;

    public RemoteMcpToolService(
        IReadOnlyList<ProtocolOperationDescriptor> operations,
        IProtocolOperationClient protocolClient)
        : this(operations, protocolClient, TimeSpan.FromSeconds(2))
    {
    }

    internal RemoteMcpToolService(
        IReadOnlyList<ProtocolOperationDescriptor> operations,
        IProtocolOperationClient protocolClient,
        TimeSpan refreshInterval)
    {
        ArgumentNullException.ThrowIfNull(operations);
        this.protocolClient = protocolClient ?? throw new ArgumentNullException(nameof(protocolClient));
        this.refreshInterval = refreshInterval <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(1)
            : refreshInterval;
        ApplyCatalog(operations);
    }

    public ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        cancellationToken.ThrowIfCancellationRequested();
        if (Environment.TickCount64 >= Volatile.Read(ref nextRefreshAt))
            return RefreshAndReturnAsync(cancellationToken);

        return ValueTask.FromResult(listToolsResult);
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        CallToolRequestParams request = requestContext.Params
            ?? throw new ArgumentException("Tool request parameters were missing.", nameof(requestContext));

        if (string.IsNullOrWhiteSpace(request.Name) ||
            !operationsByToolName.TryGetValue(request.Name, out ProtocolOperationDescriptor? operation))
        {
            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(request.Name) ||
                !operationsByToolName.TryGetValue(request.Name, out operation))
            {
                return CreateErrorResult($"Unknown tool '{request.Name}'.");
            }
        }

        try
        {
            ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromMcp(operation, request.Arguments);
            ProtocolInvocationResult result = await protocolClient.InvokeAsync(
                    operation.OperationId,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);

            JsonElement structuredPayload = ProtocolContract.DeserializePayloadElement(
                result.PayloadFormat,
                result.Payload);

            string text = string.IsNullOrWhiteSpace(result.DisplayText)
                ? GetDefaultText(structuredPayload)
                : result.DisplayText;
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = text }],
                StructuredContent = structuredPayload.ValueKind == JsonValueKind.Undefined
                    ? null
                    : structuredPayload,
                IsError = false
            };
        }
        catch (ArgumentException exception)
        {
            return CreateErrorResult(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CreateErrorResult(exception.Message);
        }
    }

    private async ValueTask<ListToolsResult> RefreshAndReturnAsync(CancellationToken cancellationToken)
    {
        await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
        return listToolsResult;
    }

    private async ValueTask RefreshCatalogAsync(CancellationToken cancellationToken)
    {
        if (Environment.TickCount64 < Volatile.Read(ref nextRefreshAt))
            return;

        Task activeRefreshTask;
        lock (syncRoot)
        {
            if (Environment.TickCount64 < nextRefreshAt)
                return;

            refreshTask ??= RefreshCatalogCoreAsync();
            activeRefreshTask = refreshTask;
        }

        await activeRefreshTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshCatalogCoreAsync()
    {
        try
        {
            DescribeOperationsResponse response = await protocolClient.DescribeOperationsAsync(CancellationToken.None).ConfigureAwait(false);
            ApplyCatalog(response.Operations);
        }
        finally
        {
            lock (syncRoot)
            {
                refreshTask = null;
            }
        }
    }

    private void ApplyCatalog(IReadOnlyList<ProtocolOperationDescriptor> catalog)
    {
        List<ProtocolOperationDescriptor> visibleOperations = new(catalog.Count);
        for (int index = 0; index < catalog.Count; index++)
        {
            ProtocolOperationDescriptor operation = catalog[index];
            if (operation.Visibility is ProtocolOperationVisibility.CliOnly ||
                operation.Hidden ||
                string.IsNullOrWhiteSpace(operation.McpToolName))
            {
                continue;
            }

            visibleOperations.Add(operation);
        }

        operations = [.. visibleOperations];
        Dictionary<string, ProtocolOperationDescriptor> nextOperationsByToolName = new(operations.Length, StringComparer.Ordinal);
        Tool[] tools = new Tool[operations.Length];
        for (int index = 0; index < operations.Length; index++)
        {
            ProtocolOperationDescriptor operation = operations[index];
            nextOperationsByToolName[operation.McpToolName!] = operation;
            tools[index] = ToTool(operation);
        }

        operationsByToolName = nextOperationsByToolName;
        listToolsResult = new ListToolsResult
        {
            Tools = tools
        };
        nextRefreshAt = Environment.TickCount64 + (long)refreshInterval.TotalMilliseconds;
    }

    private static Tool ToTool(ProtocolOperationDescriptor operation)
    {
        return new Tool
        {
            Name = operation.McpToolName!,
            Description = operation.Description,
            InputSchema = BuildInputSchema(operation)
        };
    }

    private static JsonElement BuildInputSchema(ProtocolOperationDescriptor operation)
    {
        JsonObject properties = [];
        JsonArray required = [];

        foreach (ProtocolParameterDescriptor parameter in operation.Parameters)
        {
            string propertyName = string.IsNullOrWhiteSpace(parameter.McpName) ? parameter.Name : parameter.McpName;
            properties[propertyName] = BuildParameterSchema(parameter);
            if (parameter.Required)
                required.Add(propertyName);
        }

        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0)
            schema["required"] = required;

        return JsonSerializer.SerializeToElement(schema, ProtocolContract.JsonOptions);
    }

    private static JsonObject BuildParameterSchema(ProtocolParameterDescriptor parameter)
    {
        JsonObject schema = [];
        if (parameter.IsArray)
        {
            schema["type"] = "array";
            JsonObject items = [];
            FillScalarSchema(items, parameter);
            schema["items"] = items;
        }
        else
        {
            FillScalarSchema(schema, parameter);
        }

        if (!string.IsNullOrWhiteSpace(parameter.Description))
            schema["description"] = parameter.Description;
        return schema;
    }

    private static void FillScalarSchema(JsonObject schema, ProtocolParameterDescriptor parameter)
    {
        switch (parameter.ValueKind)
        {
            case ProtocolValueKind.Flag:
                schema["type"] = "boolean";
                break;
            case ProtocolValueKind.Number:
            case ProtocolValueKind.LargeNumber:
                schema["type"] = "integer";
                break;
            case ProtocolValueKind.Real:
            case ProtocolValueKind.Fixed:
                schema["type"] = "number";
                break;
            case ProtocolValueKind.UniqueId:
                schema["type"] = "string";
                schema["format"] = "uuid";
                break;
            case ProtocolValueKind.Address:
                schema["type"] = "string";
                schema["format"] = "uri";
                break;
            case ProtocolValueKind.Timestamp:
                schema["type"] = "string";
                schema["format"] = "date-time";
                break;
            case ProtocolValueKind.Json:
                schema["type"] = "object";
                break;
            default:
                schema["type"] = "string";
                break;
        }
    }

    private static string GetDefaultText(JsonElement payload)
    {
        return payload.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
            JsonValueKind.String => payload.GetString() ?? string.Empty,
            _ => payload.GetRawText()
        };
    }

    private static CallToolResult CreateErrorResult(string message)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = message }],
            IsError = true
        };
    }
}
