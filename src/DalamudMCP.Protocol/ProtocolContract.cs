using System.Text.Json;
using System.Text.Json.Serialization;
using MemoryPack;

namespace DalamudMCP.Protocol;

public enum ProtocolPayloadFormat
{
    None = 0,
    Json = 1,
    MemoryPack = 2
}

public readonly record struct ProtocolRequestPayload(
    ProtocolPayloadFormat Format,
    byte[]? Payload)
{
    private static readonly byte[] EmptyJsonObjectBytes = [(byte)'{', (byte)'}'];

    public static ProtocolRequestPayload None { get; } = new(ProtocolPayloadFormat.None, null);

    public static ProtocolRequestPayload EmptyJsonObject { get; } = new(ProtocolPayloadFormat.Json, EmptyJsonObjectBytes);
}

[MemoryPackable]
public sealed partial record ProtocolRequestEnvelope(
    string ContractVersion,
    string RequestType,
    string RequestId,
    ProtocolPayloadFormat PayloadFormat,
    ProtocolPayloadFormat PreferredResponseFormat,
    byte[]? Payload);

[MemoryPackable]
public sealed partial record ProtocolResponseEnvelope(
    string ContractVersion,
    string RequestId,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ProtocolPayloadFormat PayloadFormat,
    byte[]? Payload,
    string? DisplayText = null);

public static class ProtocolContract
{
    public const string CurrentVersion = "2.0.0";
    public const string DefaultRequestId = "0";

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static byte[] SerializeEnvelope(ProtocolRequestEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return MemoryPackSerializer.Serialize(envelope);
    }

    public static byte[] SerializeEnvelope(ProtocolResponseEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return MemoryPackSerializer.Serialize(envelope);
    }

    public static ProtocolRequestEnvelope DeserializeRequestEnvelope(ReadOnlySpan<byte> buffer)
    {
        ProtocolRequestEnvelope? envelope = MemoryPackSerializer.Deserialize<ProtocolRequestEnvelope>(buffer);
        return envelope ?? throw new InvalidOperationException("Protocol request could not be deserialized.");
    }

    public static ProtocolResponseEnvelope DeserializeResponseEnvelope(ReadOnlySpan<byte> buffer)
    {
        ProtocolResponseEnvelope? envelope = MemoryPackSerializer.Deserialize<ProtocolResponseEnvelope>(buffer);
        return envelope ?? throw new InvalidOperationException("Protocol response could not be deserialized.");
    }

    public static T? DeserializePayload<T>(ProtocolPayloadFormat format, byte[]? payload)
    {
        return (T?)DeserializePayload(format, payload, typeof(T));
    }

    public static object? DeserializePayload(
        ProtocolPayloadFormat format,
        byte[]? payload,
        Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (format == ProtocolPayloadFormat.None || payload is null || payload.Length == 0)
            return null;

        return format switch
        {
            ProtocolPayloadFormat.Json => JsonSerializer.Deserialize(payload, targetType, JsonOptions),
            ProtocolPayloadFormat.MemoryPack => MemoryPackSerializer.Deserialize(targetType, payload),
            _ => throw new InvalidOperationException($"Unsupported payload format '{format}'.")
        };
    }

    public static JsonElement DeserializePayloadElement(
        ProtocolPayloadFormat format,
        byte[]? payload)
    {
        if (format == ProtocolPayloadFormat.None || payload is null || payload.Length == 0)
            return default;

        if (format != ProtocolPayloadFormat.Json)
            throw new InvalidOperationException($"Payload format '{format}' requires a typed target and cannot be materialized as JsonElement.");

        return JsonSerializer.Deserialize<JsonElement>(payload, JsonOptions);
    }

    public static byte[]? SerializePayload(
        object? payload,
        Type payloadType,
        ProtocolPayloadFormat format)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        if (payload is null)
            return null;

        return format switch
        {
            ProtocolPayloadFormat.None => null,
            ProtocolPayloadFormat.Json => SerializeJsonPayload(payload, payloadType),
            ProtocolPayloadFormat.MemoryPack => SerializeMemoryPackPayload(payload, payloadType),
            _ => throw new InvalidOperationException($"Unsupported payload format '{format}'.")
        };
    }

    public static ProtocolResponseEnvelope CreateSuccessResponse(
        string requestId,
        object? payload,
        Type? payloadType = null,
        string? displayText = null,
        ProtocolPayloadFormat preferredPayloadFormat = ProtocolPayloadFormat.Json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (payload is null)
        {
            return new ProtocolResponseEnvelope(
                CurrentVersion,
                requestId,
                true,
                null,
                null,
                ProtocolPayloadFormat.None,
                null,
                displayText);
        }

        Type resolvedPayloadType = payloadType ?? payload.GetType();
        ProtocolPayloadFormat payloadFormat = ResolvePayloadFormat(preferredPayloadFormat, resolvedPayloadType);
        byte[]? payloadBytes = SerializePayload(payload, resolvedPayloadType, payloadFormat);
        return new ProtocolResponseEnvelope(
            CurrentVersion,
            requestId,
            true,
            null,
            null,
            payloadFormat,
            payloadBytes,
            displayText);
    }

    public static ProtocolResponseEnvelope CreateErrorResponse(
        string requestId,
        string errorCode,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ProtocolResponseEnvelope(
            CurrentVersion,
            requestId,
            false,
            errorCode,
            errorMessage,
            ProtocolPayloadFormat.None,
            null);
    }

    public static ProtocolPayloadFormat GetPreferredMemoryPackFormat(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        return CanUseMemoryPack(payloadType)
            ? ProtocolPayloadFormat.MemoryPack
            : ProtocolPayloadFormat.Json;
    }

    public static void EnsureCompatible(string? contractVersion, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(contractVersion))
            throw new InvalidOperationException($"Unsupported contract version '<null>' for '{parameterName}'.");

        if (!string.Equals(GetMajor(CurrentVersion), GetMajor(contractVersion), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported contract version '{contractVersion}' for '{parameterName}'. Expected compatibility with '{CurrentVersion}'.");
        }
    }

    private static byte[] SerializeJsonPayload(object payload, Type payloadType)
    {
        return payload is JsonElement jsonElement
            ? JsonSerializer.SerializeToUtf8Bytes(jsonElement, JsonOptions)
            : JsonSerializer.SerializeToUtf8Bytes(payload, payloadType, JsonOptions);
    }

    private static byte[] SerializeMemoryPackPayload(object payload, Type payloadType)
    {
        if (!CanUseMemoryPack(payloadType))
            throw new InvalidOperationException($"Type '{payloadType.FullName}' is not configured for MemoryPack serialization.");

        return MemoryPackSerializer.Serialize(payloadType, payload);
    }

    private static ProtocolPayloadFormat ResolvePayloadFormat(
        ProtocolPayloadFormat preferredPayloadFormat,
        Type payloadType)
    {
        return preferredPayloadFormat == ProtocolPayloadFormat.MemoryPack && CanUseMemoryPack(payloadType)
            ? ProtocolPayloadFormat.MemoryPack
            : ProtocolPayloadFormat.Json;
    }

    private static bool CanUseMemoryPack(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        Type type = Nullable.GetUnderlyingType(payloadType) ?? payloadType;
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(Guid) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(byte[]) ||
               type.IsDefined(typeof(MemoryPackableAttribute), inherit: false);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string GetMajor(string version)
    {
        int separatorIndex = version.IndexOf('.');
        return separatorIndex < 0 ? version : version[..separatorIndex];
    }
}
