using System.Globalization;
using System.Text.Json;

namespace DalamudMCP.Plugin.Ui.Localization;

internal sealed class JsonLocalization : IUiLocalization
{
    private static readonly IReadOnlyList<UiLanguage> Languages =
    [
        new("en", "English"),
        new("ja", "日本語"),
        new("zh", "中文")
    ];

    private readonly Dictionary<string, Dictionary<string, string>> dictionaries;
    private string currentLanguage;

    public JsonLocalization(string initialLanguage = "en")
    {
        dictionaries = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = LoadDictionary("en"),
            ["ja"] = LoadDictionary("ja"),
            ["zh"] = LoadDictionary("zh")
        };
        currentLanguage = NormalizeLanguage(initialLanguage) ?? "en";
    }

    public string CurrentLanguage => currentLanguage;

    public IReadOnlyList<UiLanguage> SupportedLanguages => Languages;

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (dictionaries.TryGetValue(currentLanguage, out Dictionary<string, string>? current) &&
            current.TryGetValue(key, out string? localized))
        {
            return localized;
        }

        return dictionaries["en"].TryGetValue(key, out string? fallback)
            ? fallback
            : key;
    }

    public string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, GetString(key), args);
    }

    public void SetLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        string? normalized = NormalizeLanguage(language);
        if (normalized is not null)
            currentLanguage = normalized;
    }

    private static string? NormalizeLanguage(string language)
    {
        return Languages
            .FirstOrDefault(candidate => string.Equals(candidate.Code, language, StringComparison.OrdinalIgnoreCase))
            ?.Code;
    }

    private static Dictionary<string, string> LoadDictionary(string language)
    {
        string resourceName = $"DalamudMCP.Plugin.lang.{language}.json";
        using Stream stream = typeof(JsonLocalization).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded localization resource: {resourceName}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
