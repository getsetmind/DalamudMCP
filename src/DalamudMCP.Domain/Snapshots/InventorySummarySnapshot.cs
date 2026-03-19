namespace DalamudMCP.Domain.Snapshots;

public sealed record InventorySummarySnapshot
{
    public InventorySummarySnapshot(
        DateTimeOffset CapturedAt,
        int CurrencyGil,
        int OccupiedSlots,
        int TotalSlots,
        IReadOnlyDictionary<string, int> CategoryCounts,
        string SummaryText)
    {
        ArgumentNullException.ThrowIfNull(CategoryCounts);
        this.CapturedAt = SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        this.CurrencyGil = SnapshotGuard.NonNegative(CurrencyGil, nameof(CurrencyGil));
        this.OccupiedSlots = SnapshotGuard.NonNegative(OccupiedSlots, nameof(OccupiedSlots));
        this.TotalSlots = SnapshotGuard.NonNegative(TotalSlots, nameof(TotalSlots));
        if (this.OccupiedSlots > this.TotalSlots)
        {
            throw new ArgumentException("OccupiedSlots must be less than or equal to TotalSlots.", nameof(OccupiedSlots));
        }

        this.CategoryCounts = CategoryCounts;
        this.SummaryText = SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public DateTimeOffset CapturedAt { get; }

    public int CurrencyGil { get; }

    public int OccupiedSlots { get; }

    public int TotalSlots { get; }

    public IReadOnlyDictionary<string, int> CategoryCounts { get; }

    public string SummaryText { get; }
}
