namespace DalamudMCP.Plugin.Ui.Localization;

public interface IUiLocalization
{
    public string CurrentLanguage { get; }

    public IReadOnlyList<UiLanguage> SupportedLanguages { get; }

    public string this[string key] { get; }

    public string GetString(string key);

    public string Format(string key, params object?[] args);

    public void SetLanguage(string language);
}

public sealed record UiLanguage(string Code, string DisplayName);
