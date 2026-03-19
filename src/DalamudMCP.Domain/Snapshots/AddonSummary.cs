namespace DalamudMCP.Domain.Snapshots;

public sealed record AddonSummary
{
    public AddonSummary(
        string AddonName,
        bool IsReady,
        bool IsVisible,
        DateTimeOffset CapturedAt,
        string SummaryText)
    {
        this.AddonName = SnapshotGuard.RequiredText(AddonName, nameof(AddonName));
        this.IsReady = IsReady;
        this.IsVisible = IsVisible;
        this.CapturedAt = SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        this.SummaryText = SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public string AddonName { get; }

    public bool IsReady { get; }

    public bool IsVisible { get; }

    public DateTimeOffset CapturedAt { get; }

    public string SummaryText { get; }
}
