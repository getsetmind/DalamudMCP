namespace DalamudMCP.Domain.Snapshots;

public sealed record DutyContextSnapshot
{
    public DutyContextSnapshot(
        DateTimeOffset CapturedAt,
        int? TerritoryId,
        string? DutyName,
        string? DutyType,
        bool InDuty,
        bool IsDutyComplete,
        string SummaryText)
    {
        this.CapturedAt = SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        this.TerritoryId = TerritoryId;
        this.DutyName = DutyName;
        this.DutyType = DutyType;
        this.InDuty = InDuty;
        this.IsDutyComplete = IsDutyComplete;
        this.SummaryText = SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public DateTimeOffset CapturedAt { get; }

    public int? TerritoryId { get; }

    public string? DutyName { get; }

    public string? DutyType { get; }

    public bool InDuty { get; }

    public bool IsDutyComplete { get; }

    public string SummaryText { get; }
}
