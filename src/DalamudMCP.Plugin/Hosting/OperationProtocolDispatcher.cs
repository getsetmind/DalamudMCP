using System.Reflection;
using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Hosting;

public sealed class OperationProtocolDispatcher(
    IServiceProvider services,
    IOperationInvoker operationInvoker,
    IReadOnlyList<OperationDescriptor> operations,
    Configuration.IPluginUiConfigurationAccessor configurationStore)
{
    private const string DescribeOperationsRequestType = "__system.describe-operations";

    private readonly Dictionary<string, OperationDescriptor> operationsByRequestType = BuildRequestMap(operations);
    private readonly byte[] fullCatalogPayload = SerializeCatalog(operations);
    private readonly byte[] actionDisabledCatalogPayload = SerializeCatalog(
        PluginOperationExposurePolicy.FilterProtocolOperations(operations, enableActionOperations: false, enableUnsafeOperations: true).ToArray());
    private readonly byte[] unsafeDisabledCatalogPayload = SerializeCatalog(
        PluginOperationExposurePolicy.FilterProtocolOperations(operations, enableActionOperations: true, enableUnsafeOperations: false).ToArray());
    private readonly byte[] safeCatalogPayload = SerializeCatalog(
        PluginOperationExposurePolicy.FilterProtocolOperations(operations, enableActionOperations: false, enableUnsafeOperations: false).ToArray());

    public async ValueTask<ProtocolResponseEnvelope> DispatchAsync(
        ProtocolRequestEnvelope request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string requestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? Guid.NewGuid().ToString("N")
            : request.RequestId;

        if (string.Equals(request.RequestType, DescribeOperationsRequestType, StringComparison.Ordinal))
        {
            Configuration.PluginUiConfiguration current = configurationStore.Current;
            return new ProtocolResponseEnvelope(
                ProtocolContract.CurrentVersion,
                requestId,
                true,
                null,
                null,
                ProtocolPayloadFormat.MemoryPack,
                SelectCatalogPayload(current.EnableActionOperations, current.EnableUnsafeOperations));
        }

        if (!operationsByRequestType.TryGetValue(request.RequestType, out OperationDescriptor? descriptor) ||
            descriptor.RequestType is null)
        {
            return ProtocolContract.CreateErrorResponse(
                requestId,
                "unknown_request",
                $"Unknown protocol request '{request.RequestType}'.");
        }

        if (!PluginOperationExposurePolicy.IsEnabled(
                descriptor,
                configurationStore.Current.EnableActionOperations,
                configurationStore.Current.EnableUnsafeOperations))
        {
            return ProtocolContract.CreateErrorResponse(
                requestId,
                "disabled",
                $"Operation '{descriptor.OperationId}' is disabled. Enable action operations in the plugin settings to use it.");
        }

        try
        {
            object? typedRequest = ProtocolContract.DeserializePayload(
                request.PayloadFormat,
                request.Payload,
                descriptor.RequestType);
            if (!operationInvoker.TryInvoke(
                    descriptor.OperationId,
                    typedRequest,
                    services,
                    InvocationSurface.Protocol,
                    cancellationToken,
                    out ValueTask<OperationInvocationResult> invocation))
            {
                return ProtocolContract.CreateErrorResponse(
                    requestId,
                    "unavailable",
                    $"No operation invoker was available for '{descriptor.OperationId}'.");
            }

            OperationInvocationResult result = await invocation.ConfigureAwait(false);
            return ProtocolContract.CreateSuccessResponse(
                requestId,
                result.Result,
                result.ResultType,
                result.DisplayText,
                request.PreferredResponseFormat);
        }
        catch (ArgumentException exception)
        {
            return ProtocolContract.CreateErrorResponse(requestId, "invalid_request", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ProtocolContract.CreateErrorResponse(requestId, "unavailable", exception.Message);
        }
    }

    private static Dictionary<string, OperationDescriptor> BuildRequestMap(
        IEnumerable<OperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        Dictionary<string, OperationDescriptor> requestMap = new(StringComparer.Ordinal);
        foreach (OperationDescriptor descriptor in operations)
        {
            if (descriptor.RequestType is null)
                continue;

            foreach (string requestType in GetRequestTypes(descriptor))
            {
                requestMap[requestType] = descriptor;
            }
        }

        return requestMap;
    }

    private static IEnumerable<string> GetRequestTypes(OperationDescriptor descriptor)
    {
        yield return descriptor.OperationId;

        Type requestType = descriptor.RequestType ?? throw new InvalidOperationException(
            $"Operation '{descriptor.OperationId}' did not declare a request type.");

        ProtocolOperationAttribute? protocolOperation = requestType.GetCustomAttribute<ProtocolOperationAttribute>(inherit: false);
        if (protocolOperation is not null &&
            !string.IsNullOrWhiteSpace(protocolOperation.OperationId))
        {
            yield return protocolOperation.OperationId;
        }

        LegacyBridgeRequestAttribute? legacyRequest = requestType.GetCustomAttribute<LegacyBridgeRequestAttribute>(inherit: false);
        if (legacyRequest is not null &&
            !string.IsNullOrWhiteSpace(legacyRequest.RequestType))
        {
            yield return legacyRequest.RequestType;
        }
    }

    private static byte[] SerializeCatalog(IReadOnlyList<OperationDescriptor> catalog)
    {
        return ProtocolContract.SerializePayload(
                   ProtocolOperationCatalog.Create(catalog),
                   typeof(DescribeOperationsResponse),
                   ProtocolPayloadFormat.MemoryPack)
               ?? [];
    }

    private byte[] SelectCatalogPayload(bool enableActionOperations, bool enableUnsafeOperations)
    {
        return (enableActionOperations, enableUnsafeOperations) switch
        {
            (true, true) => fullCatalogPayload,
            (true, false) => unsafeDisabledCatalogPayload,
            (false, true) => actionDisabledCatalogPayload,
            _ => safeCatalogPayload
        };
    }
}