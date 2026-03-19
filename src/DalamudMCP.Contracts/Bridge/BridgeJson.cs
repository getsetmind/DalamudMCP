using System.Text.Json;
using System.Text.Json.Serialization;

namespace DalamudMCP.Contracts.Bridge;

public static class BridgeJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static T? DeserializePayload<T>(object? payload)
    {
        if (payload is null)
        {
            return default;
        }

        if (payload is T typedPayload)
        {
            return typedPayload;
        }

        if (payload is JsonElement element)
        {
            return element.Deserialize<T>(Options);
        }

        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(payload, Options), Options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
