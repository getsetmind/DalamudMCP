namespace DalamudMCP.Domain.Snapshots;

public sealed record PlayerContextSnapshot
{
    public PlayerContextSnapshot(
        DateTimeOffset CapturedAt,
        string CharacterName,
        string? HomeWorld,
        string? CurrentWorld,
        int? ClassJobId,
        string? ClassJobName,
        int? Level,
        int? TerritoryId,
        string? TerritoryName,
        int? MapId,
        string? MapName,
        PositionSnapshot? Position,
        bool? InCombat,
        bool? InDuty,
        bool? IsCrafting,
        bool? IsGathering,
        bool? IsMounted,
        bool? IsMoving,
        string? ZoneType,
        string? ContentStatus,
        string SummaryText)
    {
        this.CapturedAt = SnapshotGuard.RequiredCapturedAt(CapturedAt, nameof(CapturedAt));
        this.CharacterName = SnapshotGuard.RequiredText(CharacterName, nameof(CharacterName));
        this.HomeWorld = HomeWorld;
        this.CurrentWorld = CurrentWorld;
        this.ClassJobId = ClassJobId;
        this.ClassJobName = ClassJobName;
        this.Level = Level;
        this.TerritoryId = TerritoryId;
        this.TerritoryName = TerritoryName;
        this.MapId = MapId;
        this.MapName = MapName;
        this.Position = Position;
        this.InCombat = InCombat;
        this.InDuty = InDuty;
        this.IsCrafting = IsCrafting;
        this.IsGathering = IsGathering;
        this.IsMounted = IsMounted;
        this.IsMoving = IsMoving;
        this.ZoneType = ZoneType;
        this.ContentStatus = ContentStatus;
        this.SummaryText = SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public DateTimeOffset CapturedAt { get; }

    public string CharacterName { get; }

    public string? HomeWorld { get; }

    public string? CurrentWorld { get; }

    public int? ClassJobId { get; }

    public string? ClassJobName { get; }

    public int? Level { get; }

    public int? TerritoryId { get; }

    public string? TerritoryName { get; }

    public int? MapId { get; }

    public string? MapName { get; }

    public PositionSnapshot? Position { get; }

    public bool? InCombat { get; }

    public bool? InDuty { get; }

    public bool? IsCrafting { get; }

    public bool? IsGathering { get; }

    public bool? IsMounted { get; }

    public bool? IsMoving { get; }

    public string? ZoneType { get; }

    public string? ContentStatus { get; }

    public string SummaryText { get; }
}
