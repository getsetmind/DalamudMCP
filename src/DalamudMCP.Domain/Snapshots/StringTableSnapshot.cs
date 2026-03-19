namespace DalamudMCP.Domain.Snapshots;

public sealed record StringTableSnapshot
{
    public StringTableSnapshot(
        string AddonName,
        DateTimeOffset CapturedAt,
        IReadOnlyList<StringTableEntry> Entries)
    {
        ArgumentNullException.ThrowIfNull(Entries);
        this.AddonName = SnapshotGuard.RequiredText(AddonName, nameof(AddonName));
        this.CapturedAt = SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        this.Entries = Entries;
    }

    public string AddonName { get; }

    public DateTimeOffset CapturedAt { get; }

    public IReadOnlyList<StringTableEntry> Entries { get; }
}
