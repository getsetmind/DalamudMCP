using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Domain.Actions;

public sealed record NearbyInteractable
{
    public NearbyInteractable(
        string GameObjectId,
        string Name,
        string ObjectKind,
        bool IsTargetable,
        double Distance,
        double HitboxRadius,
        PositionSnapshot? Position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(GameObjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ObjectKind);
        if (Distance < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(Distance), "Distance cannot be negative.");
        }

        if (HitboxRadius < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(HitboxRadius), "HitboxRadius cannot be negative.");
        }

        this.GameObjectId = GameObjectId.Trim();
        this.Name = Name.Trim();
        this.ObjectKind = ObjectKind.Trim();
        this.IsTargetable = IsTargetable;
        this.Distance = Distance;
        this.HitboxRadius = HitboxRadius;
        this.Position = Position;
    }

    public string GameObjectId { get; }

    public string Name { get; }

    public string ObjectKind { get; }

    public bool IsTargetable { get; }

    public double Distance { get; }

    public double HitboxRadius { get; }

    public PositionSnapshot? Position { get; }
}
