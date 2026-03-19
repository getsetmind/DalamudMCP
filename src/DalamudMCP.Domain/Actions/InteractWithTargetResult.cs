namespace DalamudMCP.Domain.Actions;

public sealed record InteractWithTargetResult
{
    public InteractWithTargetResult(
        string? ExpectedGameObjectId,
        bool Succeeded,
        string? Reason,
        string? InteractedGameObjectId,
        string? TargetName,
        string? ObjectKind,
        double? Distance,
        string SummaryText)
    {
        this.ExpectedGameObjectId = string.IsNullOrWhiteSpace(ExpectedGameObjectId) ? null : ExpectedGameObjectId.Trim();
        this.Succeeded = Succeeded;
        this.Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();
        this.InteractedGameObjectId = string.IsNullOrWhiteSpace(InteractedGameObjectId) ? null : InteractedGameObjectId.Trim();
        this.TargetName = string.IsNullOrWhiteSpace(TargetName) ? null : TargetName.Trim();
        this.ObjectKind = string.IsNullOrWhiteSpace(ObjectKind) ? null : ObjectKind.Trim();
        this.Distance = Distance;
        this.SummaryText = Snapshots.SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public string? ExpectedGameObjectId { get; }

    public bool Succeeded { get; }

    public string? Reason { get; }

    public string? InteractedGameObjectId { get; }

    public string? TargetName { get; }

    public string? ObjectKind { get; }

    public double? Distance { get; }

    public string SummaryText { get; }
}
