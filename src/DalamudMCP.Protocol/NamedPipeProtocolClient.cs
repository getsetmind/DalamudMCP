using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;

namespace DalamudMCP.Protocol;

public sealed class NamedPipeProtocolClient(string pipeName, TimeSpan? connectTimeout = null) : IProtocolOperationClient
{
    private static readonly ConcurrentDictionary<EmptyRequestCacheKey, byte[]> EmptyRequestCache = new();
    private static readonly ConcurrentDictionary<Type, bool> EmptyRequestShapeCache = new();
    private static readonly ConcurrentDictionary<Type, string> RequestTypeCache = new();
    private readonly string pipeName = string.IsNullOrWhiteSpace(pipeName)
        ? throw new ArgumentException("Pipe name must be non-empty.", nameof(pipeName))
        : pipeName.Trim();

    private readonly TimeSpan connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(1);

    public ValueTask<ProtocolInvocationResult> InvokeAsync(
        string requestType,
        ProtocolRequestPayload request,
        CancellationToken cancellationToken)
    {
        return InvokeCoreAsync(
            requestType,
            request,
            preferredResponseFormat: ProtocolPayloadFormat.Json,
            cancellationToken);
    }

    public async ValueTask<TResponse> InvokeAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);

        ProtocolPayloadFormat requestPayloadFormat = ProtocolContract.GetPreferredMemoryPackFormat(typeof(TRequest));
        ProtocolPayloadFormat preferredResponseFormat = ProtocolContract.GetPreferredMemoryPackFormat(typeof(TResponse));
        string requestType = ResolveRequestType(typeof(TRequest));
        ProtocolRequestPayload requestPayload = CanUseCachedEmptyRequest(request)
            ? ProtocolRequestPayload.None
            : new(
                requestPayloadFormat,
                ProtocolContract.SerializePayload(request, request.GetType(), requestPayloadFormat));
        ProtocolInvocationResult result = await InvokeCoreAsync(
                requestType,
                requestPayload,
                preferredResponseFormat,
                cancellationToken)
            .ConfigureAwait(false);

        TResponse? payload = ProtocolContract.DeserializePayload<TResponse>(result.PayloadFormat, result.Payload);
        if (payload is null)
            throw new InvalidOperationException("Protocol response payload was empty or invalid.");

        return payload;
    }

    public async ValueTask<DescribeOperationsResponse> DescribeOperationsAsync(CancellationToken cancellationToken)
    {
        ProtocolInvocationResult result = await InvokeCoreAsync(
                "__system.describe-operations",
                ProtocolRequestPayload.None,
                ProtocolPayloadFormat.MemoryPack,
                cancellationToken)
            .ConfigureAwait(false);

        DescribeOperationsResponse? response = ProtocolContract.DeserializePayload<DescribeOperationsResponse>(
            result.PayloadFormat,
            result.Payload);
        if (response is null)
            throw new InvalidOperationException("Protocol operation catalog payload was empty or invalid.");

        return response;
    }

    private async ValueTask<ProtocolInvocationResult> InvokeCoreAsync(
        string requestType,
        ProtocolRequestPayload request,
        ProtocolPayloadFormat preferredResponseFormat,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestType);

        await using NamedPipeClientStream client = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync((int)connectTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

        string normalizedRequestType = requestType.Trim();
        byte[] requestBytes = request.Format == ProtocolPayloadFormat.None || request.Payload is null || request.Payload.Length == 0
            ? EmptyRequestCache.GetOrAdd(
                new EmptyRequestCacheKey(normalizedRequestType, preferredResponseFormat),
                static key => ProtocolContract.SerializeEnvelope(new ProtocolRequestEnvelope(
                    ProtocolContract.CurrentVersion,
                    key.RequestType,
                    ProtocolContract.DefaultRequestId,
                    ProtocolPayloadFormat.None,
                    key.PreferredResponseFormat,
                    null)))
            : ProtocolContract.SerializeEnvelope(new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                normalizedRequestType,
                ProtocolContract.DefaultRequestId,
                request.Format,
                preferredResponseFormat,
                request.Payload));
        await WriteFrameAsync(client, requestBytes, cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);

        byte[] responseBytes = await ReadFrameAsync(client, cancellationToken).ConfigureAwait(false);
        ProtocolResponseEnvelope response = ProtocolContract.DeserializeResponseEnvelope(responseBytes);
        ProtocolContract.EnsureCompatible(response.ContractVersion, nameof(response.ContractVersion));

        if (!response.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? response.ErrorCode ?? "Protocol request failed."
                    : response.ErrorMessage);
        }

        return new ProtocolInvocationResult(response.PayloadFormat, response.Payload, response.DisplayText);
    }

    private static string ResolveRequestType(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return RequestTypeCache.GetOrAdd(requestType, static type => ResolveRequestTypeCore(type));
    }

    private static bool CanUseCachedEmptyRequest(object? request)
    {
        if (request is null)
            return true;

        Type requestType = request.GetType();
        return EmptyRequestShapeCache.GetOrAdd(
            requestType,
            static type => type.GetProperties().Length == 0);
    }

    private static string ResolveRequestTypeCore(Type requestType)
    {
        ProtocolOperationAttribute? protocolBinding = requestType.GetCustomAttributes(typeof(ProtocolOperationAttribute), false)
            .OfType<ProtocolOperationAttribute>()
            .SingleOrDefault();
        if (!string.IsNullOrWhiteSpace(protocolBinding?.OperationId))
            return protocolBinding.OperationId;

        LegacyBridgeRequestAttribute? legacyBinding = requestType.GetCustomAttributes(typeof(LegacyBridgeRequestAttribute), false)
            .OfType<LegacyBridgeRequestAttribute>()
            .SingleOrDefault();
        if (!string.IsNullOrWhiteSpace(legacyBinding?.RequestType))
            return legacyBinding.RequestType;

        throw new InvalidOperationException(
            $"The request type '{requestType.FullName}' is missing [{nameof(ProtocolOperationAttribute)}] or [{nameof(LegacyBridgeRequestAttribute)}].");
    }

    private static async Task WriteFrameAsync(PipeStream stream, byte[] payload, CancellationToken cancellationToken)
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(sizeof(int));
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            await stream.WriteAsync(header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    private static async Task<byte[]> ReadFrameAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(sizeof(int));
        int length;
        try
        {
            await ReadExactAsync(stream, header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            length = BinaryPrimitives.ReadInt32LittleEndian(header);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }

        if (length <= 0)
            throw new InvalidOperationException("Protocol response length was invalid.");

        byte[] payload = new byte[length];
        await ReadExactAsync(stream, payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer[offset..], cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
                throw new InvalidOperationException("Protocol stream closed unexpectedly.");

            offset += bytesRead;
        }
    }

    private readonly record struct EmptyRequestCacheKey(
        string RequestType,
        ProtocolPayloadFormat PreferredResponseFormat);
}
