namespace DalamudMCP.Plugin.Ui.Localization;

internal sealed class EnglishLocalization : IUiLocalization
{
    public static readonly EnglishLocalization Instance = new();

    private EnglishLocalization()
    {
    }

    public string CurrentLanguage => "en";

    public IReadOnlyList<UiLanguage> SupportedLanguages { get; } = [new("en", "English")];

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key switch
        {
            "status.server_running" => "Server status: running",
            "status.server_stopped" => "Server status: stopped",
            "status.http_running" => "Status: running",
            "status.http_stopped" => "Status: stopped",
            "status.actions_enabled" => "Action operations: enabled",
            "status.actions_disabled" => "Action operations: disabled",
            "status.unsafe_enabled" => "Unsafe operations: enabled",
            "status.unsafe_disabled" => "Unsafe operations: disabled",
            "label.pipe_name" => "Active pipe (advanced): {0}",
            "label.endpoint" => "Endpoint: {0}",
            "label.last_error" => "Last error: {0}",
            "label.cli_prefix" => "CLI: {0}",
            "label.mcp_prefix" => "MCP: {0}",
            "status.reader_format" => "Reader status: {0}/{1} ready",
            "status.exposure_unsafe_pending" => "Exposure: disabled until unsafe operations are enabled",
            "status.exposure_action_pending" => "Exposure: disabled until action operations are enabled",
            "status.reader_ready_word" => "ready",
            "status.reader_not_ready_word" => "not ready",
            "status.reader" => "Reader: {0}",
            "status.reader_detail" => "Reader: {0} ({1})",
            _ => key
        };
    }

    public string Format(string key, params object?[] args)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, GetString(key), args);
    }

    public void SetLanguage(string language)
    {
        _ = language;
    }
}
