namespace DalamudMCP.Domain.Snapshots;

public sealed record PositionSnapshot
{
    public PositionSnapshot(double? X, double? Y, double? Z, string Precision)
    {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
        this.Precision = SnapshotGuard.RequiredText(Precision, nameof(Precision));
    }

    public double? X { get; }

    public double? Y { get; }

    public double? Z { get; }

    public string Precision { get; }
}
