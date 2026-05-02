using DalamudMCP.Plugin.Ui.Localization;

namespace DalamudMCP.Plugin.Tests;

public sealed class JsonLocalizationTests
{
    [Fact]
    public void GetString_returns_selected_language_value()
    {
        JsonLocalization localization = new("ja");

        Assert.Equal("DalamudMCP 設定", localization["window.title"]);
    }

    [Fact]
    public void SetLanguage_ignores_unsupported_language()
    {
        JsonLocalization localization = new("en");

        localization.SetLanguage("missing");

        Assert.Equal("en", localization.CurrentLanguage);
    }

    [Fact]
    public void SetLanguage_normalizes_supported_language_code()
    {
        JsonLocalization localization = new("en");

        localization.SetLanguage("JA");

        Assert.Equal("ja", localization.CurrentLanguage);
    }

    [Fact]
    public void GetString_returns_key_when_missing_from_all_languages()
    {
        JsonLocalization localization = new("zh");

        Assert.Equal("missing.key", localization["missing.key"]);
    }

    [Fact]
    public void Format_uses_localized_template()
    {
        JsonLocalization localization = new("en");

        string value = localization.Format("header.exposed", 3, 10);

        Assert.Equal("3/10 EXPOSED", value);
    }
}
