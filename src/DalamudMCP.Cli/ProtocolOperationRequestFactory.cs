using System.Buffers;
using System.Globalization;
using System.Text.Json;
using DalamudMCP.Framework.Cli;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli;

internal static class ProtocolOperationRequestFactory
{
    private static readonly IDictionary<string, JsonElement> EmptyJsonArguments =
        new Dictionary<string, JsonElement>(0, StringComparer.Ordinal);

    public static ProtocolRequestPayload CreateFromCli(
        ProtocolOperationDescriptor operation,
        IReadOnlyDictionary<string, string> options,
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(arguments);

        if (operation.Parameters.Count == 0)
            return ProtocolRequestPayload.None;

        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        bool wroteAny = false;
        foreach (ProtocolParameterDescriptor parameter in operation.Parameters)
        {
            switch (parameter.Source)
            {
                case ProtocolParameterSource.Option:
                    if (!CliBinding.TryFindOptionValue(options, GetCliLookupName(parameter), parameter.Aliases, out string? optionValue))
                    {
                        if (parameter.Required)
                            throw new ArgumentException($"Missing required --{GetCliLookupName(parameter)} option.");
                        continue;
                    }

                    writer.WritePropertyName(parameter.Name);
                    WriteTextValue(writer, parameter, optionValue!);
                    wroteAny = true;
                    break;

                case ProtocolParameterSource.Argument:
                    if (parameter.Position is null || parameter.Position.Value < 0 || parameter.Position.Value >= arguments.Count)
                    {
                        if (parameter.Required)
                            throw new ArgumentException($"Missing required argument '{GetCliLookupName(parameter)}'.");
                        continue;
                    }

                    writer.WritePropertyName(parameter.Name);
                    WriteTextValue(writer, parameter, arguments[parameter.Position.Value]);
                    wroteAny = true;
                    break;

                default:
                    throw new InvalidOperationException($"Parameter '{parameter.Name}' had unsupported source '{parameter.Source}'.");
            }
        }

        writer.WriteEndObject();
        writer.Flush();
        return wroteAny
            ? new ProtocolRequestPayload(ProtocolPayloadFormat.Json, buffer.WrittenSpan.ToArray())
            : ProtocolRequestPayload.EmptyJsonObject;
    }

    public static ProtocolRequestPayload CreateFromMcp(
        ProtocolOperationDescriptor operation,
        IDictionary<string, JsonElement>? arguments)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Parameters.Count == 0)
            return ProtocolRequestPayload.None;

        IDictionary<string, JsonElement> providedArguments = arguments ?? EmptyJsonArguments;
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        bool wroteAny = false;
        foreach (ProtocolParameterDescriptor parameter in operation.Parameters)
        {
            string lookupName = GetMcpLookupName(parameter);
            if (!providedArguments.TryGetValue(lookupName, out JsonElement value))
            {
                if (parameter.Required)
                    throw new ArgumentException($"Missing required '{lookupName}' argument.");
                continue;
            }

            writer.WritePropertyName(parameter.Name);
            WriteJsonValue(writer, parameter, value);
            wroteAny = true;
        }

        writer.WriteEndObject();
        writer.Flush();
        return wroteAny
            ? new ProtocolRequestPayload(ProtocolPayloadFormat.Json, buffer.WrittenSpan.ToArray())
            : ProtocolRequestPayload.EmptyJsonObject;
    }

    private static string GetCliLookupName(ProtocolParameterDescriptor parameter)
    {
        return string.IsNullOrWhiteSpace(parameter.CliName) ? parameter.Name : parameter.CliName;
    }

    private static string GetMcpLookupName(ProtocolParameterDescriptor parameter)
    {
        return string.IsNullOrWhiteSpace(parameter.McpName) ? parameter.Name : parameter.McpName;
    }

    private static void WriteTextValue(Utf8JsonWriter writer, ProtocolParameterDescriptor parameter, string text)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(text);

        if (parameter.IsArray)
        {
            writer.WriteStartArray();
            WriteDelimitedSegments(writer, parameter, text.AsSpan());
            writer.WriteEndArray();
            return;
        }

        if (string.IsNullOrWhiteSpace(text) && parameter.IsNullable)
        {
            writer.WriteNullValue();
            return;
        }

        WriteScalarTextValue(writer, parameter, text.AsSpan(), text);
    }

    private static void WriteDelimitedSegments(
        Utf8JsonWriter writer,
        ProtocolParameterDescriptor parameter,
        ReadOnlySpan<char> text)
    {
        int segmentStart = 0;
        while (segmentStart <= text.Length)
        {
            int separatorIndex = text[segmentStart..].IndexOf(',');
            ReadOnlySpan<char> segment = separatorIndex >= 0
                ? text.Slice(segmentStart, separatorIndex)
                : text[segmentStart..];
            ReadOnlySpan<char> trimmedSegment = Trim(segment);
            if (!trimmedSegment.IsEmpty)
                WriteScalarTextValue(writer, parameter, trimmedSegment, trimmedSegment.ToString());

            if (separatorIndex < 0)
                break;

            segmentStart += separatorIndex + 1;
        }
    }

    private static void WriteScalarTextValue(
        Utf8JsonWriter writer,
        ProtocolParameterDescriptor parameter,
        ReadOnlySpan<char> text,
        string originalText)
    {
        try
        {
            switch (parameter.ValueKind)
            {
                case ProtocolValueKind.Text:
                    writer.WriteStringValue(text);
                    return;

                case ProtocolValueKind.Flag:
                    if (bool.TryParse(text, out bool parsedBool))
                    {
                        writer.WriteBooleanValue(parsedBool);
                        return;
                    }

                    break;

                case ProtocolValueKind.Number:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                    {
                        writer.WriteNumberValue(parsedInt);
                        return;
                    }

                    break;

                case ProtocolValueKind.LargeNumber:
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedLong))
                    {
                        writer.WriteNumberValue(parsedLong);
                        return;
                    }

                    break;

                case ProtocolValueKind.Real:
                    if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedDouble))
                    {
                        writer.WriteNumberValue(parsedDouble);
                        return;
                    }

                    break;

                case ProtocolValueKind.Fixed:
                    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedDecimal))
                    {
                        writer.WriteNumberValue(parsedDecimal);
                        return;
                    }

                    break;

                case ProtocolValueKind.UniqueId:
                    if (Guid.TryParse(text, out Guid parsedGuid))
                    {
                        writer.WriteStringValue(parsedGuid);
                        return;
                    }

                    break;

                case ProtocolValueKind.Address:
                    if (Uri.TryCreate(originalText, UriKind.RelativeOrAbsolute, out Uri? parsedUri))
                    {
                        writer.WriteStringValue(parsedUri.ToString());
                        return;
                    }

                    break;

                case ProtocolValueKind.Timestamp:
                    if (DateTimeOffset.TryParse(originalText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsedDateTimeOffset))
                    {
                        writer.WriteStringValue(parsedDateTimeOffset);
                        return;
                    }

                    break;

                case ProtocolValueKind.Json:
                    {
                        using JsonDocument document = JsonDocument.Parse(originalText);
                        document.RootElement.WriteTo(writer);
                        return;
                    }
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException or UriFormatException)
        {
            throw new ArgumentException($"The value '{originalText}' is not valid for '{parameter.Name}'.", exception);
        }

        throw new ArgumentException($"The value '{originalText}' is not valid for '{parameter.Name}'.");
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, ProtocolParameterDescriptor parameter, JsonElement element)
    {
        if (parameter.IsArray)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new ArgumentException($"The value for '{parameter.Name}' must be an array.");

            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray())
                WriteJsonScalarValue(writer, parameter, item);

            writer.WriteEndArray();
            return;
        }

        if (element.ValueKind == JsonValueKind.Null && parameter.IsNullable)
        {
            writer.WriteNullValue();
            return;
        }

        WriteJsonScalarValue(writer, parameter, element);
    }

    private static void WriteJsonScalarValue(Utf8JsonWriter writer, ProtocolParameterDescriptor parameter, JsonElement element)
    {
        try
        {
            switch (parameter.ValueKind)
            {
                case ProtocolValueKind.Text:
                    writer.WriteStringValue(element.GetString() ?? string.Empty);
                    return;

                case ProtocolValueKind.Flag:
                    writer.WriteBooleanValue(element.GetBoolean());
                    return;

                case ProtocolValueKind.Number:
                    writer.WriteNumberValue(element.GetInt32());
                    return;

                case ProtocolValueKind.LargeNumber:
                    writer.WriteNumberValue(element.GetInt64());
                    return;

                case ProtocolValueKind.Real:
                    writer.WriteNumberValue(element.GetDouble());
                    return;

                case ProtocolValueKind.Fixed:
                    writer.WriteNumberValue(element.GetDecimal());
                    return;

                case ProtocolValueKind.UniqueId:
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteStringValue(Guid.Parse(element.GetString()!));
                        return;
                    }

                    break;

                case ProtocolValueKind.Address:
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteStringValue(new Uri(element.GetString()!, UriKind.RelativeOrAbsolute).ToString());
                        return;
                    }

                    break;

                case ProtocolValueKind.Timestamp:
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteStringValue(DateTimeOffset.Parse(element.GetString()!, CultureInfo.InvariantCulture));
                        return;
                    }

                    break;

                default:
                    element.WriteTo(writer);
                    return;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException or UriFormatException)
        {
            throw new ArgumentException($"The value for '{parameter.Name}' was not valid.", exception);
        }

        throw new ArgumentException($"The value for '{parameter.Name}' was not valid.");
    }

    private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
    {
        int start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
            start++;

        int end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
            end--;

        return start > end
            ? ReadOnlySpan<char>.Empty
            : value[start..(end + 1)];
    }
}
