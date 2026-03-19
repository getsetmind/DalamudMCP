namespace DalamudMCP.Domain.Snapshots;

public sealed record AddonTreeSnapshot
{
    public AddonTreeSnapshot(
        string AddonName,
        DateTimeOffset CapturedAt,
        IReadOnlyList<NodeSnapshot> Roots)
    {
        ArgumentNullException.ThrowIfNull(Roots);
        this.AddonName = SnapshotGuard.RequiredText(AddonName, nameof(AddonName));
        this.CapturedAt = SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        this.Roots = Roots;
    }

    public string AddonName { get; }

    public DateTimeOffset CapturedAt { get; }

    public IReadOnlyList<NodeSnapshot> Roots { get; }
}
