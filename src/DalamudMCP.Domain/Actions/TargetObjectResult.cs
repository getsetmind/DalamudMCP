namespace DalamudMCP.Domain.Actions;

public sealed record TargetObjectResult
{
    public TargetObjectResult(
        string RequestedGameObjectId,
        bool Succeeded,
        string? Reason,
        string? TargetedGameObjectId,
        string? TargetName,
        string? ObjectKind,
        string SummaryText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RequestedGameObjectId);
        this.RequestedGameObjectId = RequestedGameObjectId.Trim();
        this.Succeeded = Succeeded;
        this.Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();
        this.TargetedGameObjectId = string.IsNullOrWhiteSpace(TargetedGameObjectId) ? null : TargetedGameObjectId.Trim();
        this.TargetName = string.IsNullOrWhiteSpace(TargetName) ? null : TargetName.Trim();
        this.ObjectKind = string.IsNullOrWhiteSpace(ObjectKind) ? null : ObjectKind.Trim();
        this.SummaryText = Snapshots.SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public string RequestedGameObjectId { get; }

    public bool Succeeded { get; }

    public string? Reason { get; }

    public string? TargetedGameObjectId { get; }

    public string? TargetName { get; }

    public string? ObjectKind { get; }

    public string SummaryText { get; }
}
