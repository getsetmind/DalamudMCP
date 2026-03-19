namespace DalamudMCP.Domain.Actions;

public sealed record NearbyInteractablesSnapshot
{
    public NearbyInteractablesSnapshot(
        DateTimeOffset CapturedAt,
        double MaxDistance,
        IReadOnlyList<NearbyInteractable> Interactables,
        string SummaryText)
    {
        this.CapturedAt = Snapshots.SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        if (MaxDistance <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDistance), "MaxDistance must be positive.");
        }

        ArgumentNullException.ThrowIfNull(Interactables);
        this.MaxDistance = MaxDistance;
        this.Interactables = Interactables;
        this.SummaryText = Snapshots.SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public DateTimeOffset CapturedAt { get; }

    public double MaxDistance { get; }

    public IReadOnlyList<NearbyInteractable> Interactables { get; }

    public string SummaryText { get; }
}
