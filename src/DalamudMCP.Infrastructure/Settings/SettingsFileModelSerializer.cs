using System.Text.Json;
using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Infrastructure.Settings;

internal static class SettingsFileModelSerializer
{
    public static async Task<ExposurePolicy> DeserializePolicyAsync(Stream stream, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToPolicy(document.RootElement);
    }

    public static SettingsFileModel FromPolicy(ExposurePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new SettingsFileModel(
            Version: SettingsFileModel.CurrentVersion,
            ObservationProfileEnabled: policy.ObservationProfileEnabled,
            ActionProfileEnabled: policy.ActionProfileEnabled,
            EnabledTools: policy.EnabledTools.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            EnabledResources: policy.EnabledResources.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            EnabledAddons: policy.EnabledAddons.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static ExposurePolicy ToPolicy(JsonElement root)
    {
        var observationProfileEnabled = ReadBoolean(root, defaultValue: true, "observationProfileEnabled", "baselineProfileEnabled");
        var actionProfileEnabled = ReadBoolean(root, defaultValue: false, "actionProfileEnabled", "experimentalProfileEnabled");

        return new ExposurePolicy(
            ReadStringArray(root, "enabledTools"),
            ReadStringArray(root, "enabledResources"),
            ReadStringArray(root, "enabledAddons"),
            observationProfileEnabled,
            actionProfileEnabled);
    }

    private static bool ReadBoolean(JsonElement root, bool defaultValue, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(root, propertyName, out var property) &&
                (property.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return property.GetBoolean();
            }
        }

        return defaultValue;
    }

    private static string[] ReadStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var property) || property.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString())
            .OfType<string>()
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
