namespace DalamudMCP.Domain.Snapshots;

public sealed record StringTableEntry
{
    public StringTableEntry(int Index, string? RawValue, string? DecodedValue)
    {
        this.Index = SnapshotGuard.NonNegative(Index, nameof(Index));
        this.RawValue = RawValue;
        this.DecodedValue = DecodedValue;
    }

    public int Index { get; }

    public string? RawValue { get; }

    public string? DecodedValue { get; }
}
