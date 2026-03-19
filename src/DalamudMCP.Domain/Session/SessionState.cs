namespace DalamudMCP.Domain.Session;

public sealed record SessionState
{
    public SessionState(
        DateTimeOffset capturedAt,
        string pipeName,
        bool isBridgeServerRunning,
        IEnumerable<SessionComponentState>? components,
        string summaryText)
    {
        CapturedAt = capturedAt;
        PipeName = NormalizePipeName(pipeName);
        IsBridgeServerRunning = isBridgeServerRunning;
        Components = NormalizeComponents(components);
        SummaryText = NormalizeSummaryText(summaryText);
    }

    public DateTimeOffset CapturedAt { get; }

    public string PipeName { get; }

    public bool IsBridgeServerRunning { get; }

    public IReadOnlyList<SessionComponentState> Components { get; }

    public string SummaryText { get; }

    public int ReadyComponentCount => Components.Count(static component => component.IsReady);

    public int TotalComponentCount => Components.Count;

    private static string NormalizePipeName(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("PipeName cannot be null or whitespace.", nameof(pipeName));
        }

        return pipeName.Trim();
    }

    private static SessionComponentState[] NormalizeComponents(IEnumerable<SessionComponentState>? components)
    {
        if (components is null)
        {
            return [];
        }

        return components
            .GroupBy(static component => component.ComponentName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(static grouping => grouping.Last())
            .OrderBy(static component => component.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeSummaryText(string summaryText)
    {
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            throw new ArgumentException("SummaryText cannot be null or whitespace.", nameof(summaryText));
        }

        return summaryText.Trim();
    }
}
