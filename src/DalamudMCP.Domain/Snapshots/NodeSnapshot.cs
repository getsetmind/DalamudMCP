namespace DalamudMCP.Domain.Snapshots;

public sealed record NodeSnapshot
{
    public NodeSnapshot(
        int NodeId,
        string NodeType,
        bool Visible,
        float X,
        float Y,
        float Width,
        float Height,
        string? Text,
        IReadOnlyList<NodeSnapshot> Children)
    {
        ArgumentNullException.ThrowIfNull(Children);
        this.NodeId = NodeId;
        this.NodeType = SnapshotGuard.RequiredText(NodeType, nameof(NodeType));
        this.Visible = Visible;
        this.X = X;
        this.Y = Y;
        this.Width = SnapshotGuard.NonNegative(Width, nameof(Width));
        this.Height = SnapshotGuard.NonNegative(Height, nameof(Height));
        this.Text = Text;
        this.Children = Children;
    }

    public int NodeId { get; }

    public string NodeType { get; }

    public bool Visible { get; }

    public float X { get; }

    public float Y { get; }

    public float Width { get; }

    public float Height { get; }

    public string? Text { get; }

    public IReadOnlyList<NodeSnapshot> Children { get; }
}
