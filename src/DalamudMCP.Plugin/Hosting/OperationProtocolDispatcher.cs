using System.Diagnostics;
using System.Reflection;
using DalamudMCP.Protocol;
using Manifold;

namespace DalamudMCP.Plugin.Hosting;

public sealed class OperationProtocolDispatcher(
    IServiceProvider services,
    IOperationInvoker operationInvoker,
    IReadOnlyList<OperationDescriptor> operations,
    Configuration.IPluginUiConfigurationAccessor configurationStore,
    OperationAuditLog? auditLog = null,
    CapabilityApprovalService? approvalService = null,
    CapabilityRateLimiter? rateLimiter = null)
{
    private const string DescribeOperationsRequestType = "__system.describe-operations";

    private readonly Dictionary<string, OperationDescriptor> operationsByRequestType = BuildRequestMap(operations);

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
                SerializeCatalog(PluginOperationExposurePolicy.FilterProtocolOperations(operations, current).ToArray()));
        }

        if (!operationsByRequestType.TryGetValue(request.RequestType, out OperationDescriptor? descriptor) ||
            descriptor.RequestType is null)
        {
            return ProtocolContract.CreateErrorResponse(
                requestId,
                "unknown_request",
                $"Unknown protocol request '{request.RequestType}'.");
        }

        long startedAt = Stopwatch.GetTimestamp();
        object? typedRequest = null;
        CapabilityAuthorizationDecision? authorization = null;
        try
        {
            typedRequest = ProtocolContract.DeserializePayload(
                request.PayloadFormat,
                request.Payload,
                descriptor.RequestType);
            authorization = PluginOperationExposurePolicy.Authorize(
                descriptor,
                configurationStore.Current,
                typedRequest);
            if (authorization.RequiresConfirmation && approvalService is not null)
                authorization = approvalService.AuthorizeOrQueue(descriptor, typedRequest, authorization);

            if (authorization.IsAllowed &&
                rateLimiter is not null &&
                !rateLimiter.TryAcquire(
                    authorization.PermissionScope,
                    typedRequest,
                    authorization.MaximumCallsPerMinute,
                    out TimeSpan retryAfter))
            {
                authorization = CapabilityAuthorizationDecision.Denied(
                    authorization.PermissionScope,
                    $"Capability rate limit exceeded. Retry after {Math.Ceiling(retryAfter.TotalSeconds)} seconds.");
            }

            if (!authorization.IsAllowed)
            {
                string errorCode = authorization.RequiresConfirmation ? "confirmation_required" : "permission_denied";
                await WriteAuditAsync(
                    descriptor,
                    requestId,
                    typedRequest,
                    result: null,
                    authorization,
                    succeeded: false,
                    errorCode,
                    (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    cancellationToken).ConfigureAwait(false);
                return ProtocolContract.CreateErrorResponse(requestId, errorCode, authorization.Reason!);
            }

            if (!operationInvoker.TryInvoke(
                    descriptor.OperationId,
                    typedRequest,
                    services,
                    InvocationSurface.Protocol,
                    cancellationToken,
                    out ValueTask<OperationInvocationResult> invocation))
            {
                await WriteAuditAsync(
                    descriptor,
                    requestId,
                    typedRequest,
                    result: null,
                    authorization,
                    succeeded: false,
                    "unavailable",
                    (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    cancellationToken).ConfigureAwait(false);
                return ProtocolContract.CreateErrorResponse(
                    requestId,
                    "unavailable",
                    $"No operation invoker was available for '{descriptor.OperationId}'.");
            }

            OperationInvocationResult result = await invocation.ConfigureAwait(false);
            bool domainSucceeded = IsDomainSuccess(result.Result);
            await WriteAuditAsync(
                descriptor,
                requestId,
                typedRequest,
                result.Result,
                authorization,
                domainSucceeded,
                domainSucceeded ? null : "domain_failure",
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                cancellationToken).ConfigureAwait(false);
            return ProtocolContract.CreateSuccessResponse(
                requestId,
                result.Result,
                result.ResultType,
                result.DisplayText,
                request.PreferredResponseFormat);
        }
        catch (ArgumentException exception)
        {
            await WriteFailedAuditAsync(
                descriptor,
                requestId,
                typedRequest,
                authorization,
                "invalid_request",
                startedAt,
                cancellationToken).ConfigureAwait(false);
            return ProtocolContract.CreateErrorResponse(requestId, "invalid_request", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteFailedAuditAsync(
                descriptor,
                requestId,
                typedRequest,
                authorization,
                "unavailable",
                startedAt,
                cancellationToken).ConfigureAwait(false);
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

    private static bool IsDomainSuccess(object? result)
    {
        if (result is null)
            return true;

        PropertyInfo? successProperty = result.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(static property =>
                property.PropertyType == typeof(bool) &&
                (string.Equals(property.Name, "Success", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(property.Name, "Succeeded", StringComparison.OrdinalIgnoreCase)));
        return successProperty?.GetValue(result) is not bool succeeded || succeeded;
    }

    private ValueTask WriteAuditAsync(
        OperationDescriptor operation,
        string requestId,
        object? request,
        object? result,
        CapabilityAuthorizationDecision authorization,
        bool succeeded,
        string? errorCode,
        long elapsedMilliseconds,
        CancellationToken cancellationToken)
    {
        return auditLog is null
            ? ValueTask.CompletedTask
            : auditLog.WriteAsync(
                operation,
                requestId,
                request,
                result,
                authorization,
                succeeded,
                errorCode,
                elapsedMilliseconds,
                cancellationToken);
    }

    private ValueTask WriteFailedAuditAsync(
        OperationDescriptor operation,
        string requestId,
        object? request,
        CapabilityAuthorizationDecision? authorization,
        string errorCode,
        long startedAt,
        CancellationToken cancellationToken)
    {
        CapabilityAuthorizationDecision effectiveAuthorization = authorization ?? CapabilityAuthorizationDecision.Denied(
            PluginOperationMetadataCatalog.Resolve(operation.OperationId).PermissionScope,
            "The request failed before authorization completed.");
        return WriteAuditAsync(
            operation,
            requestId,
            request,
            result: null,
            effectiveAuthorization,
            succeeded: false,
            errorCode,
            (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            cancellationToken);
    }
}
