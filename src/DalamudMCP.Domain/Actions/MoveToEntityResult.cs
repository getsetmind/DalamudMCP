using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Domain.Actions;

public sealed record MoveToEntityResult
{
    public MoveToEntityResult(
        string RequestedGameObjectId,
        bool Succeeded,
        string? Reason,
        string? ResolvedGameObjectId,
        string? TargetName,
        string? ObjectKind,
        PositionSnapshot? Destination,
        string SummaryText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RequestedGameObjectId);
        this.RequestedGameObjectId = RequestedGameObjectId.Trim();
        this.Succeeded = Succeeded;
        this.Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();
        this.ResolvedGameObjectId = string.IsNullOrWhiteSpace(ResolvedGameObjectId) ? null : ResolvedGameObjectId.Trim();
        this.TargetName = string.IsNullOrWhiteSpace(TargetName) ? null : TargetName.Trim();
        this.ObjectKind = string.IsNullOrWhiteSpace(ObjectKind) ? null : ObjectKind.Trim();
        this.Destination = Destination;
        this.SummaryText = Snapshots.SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public string RequestedGameObjectId { get; }

    public bool Succeeded { get; }

    public string? Reason { get; }

    public string? ResolvedGameObjectId { get; }

    public string? TargetName { get; }

    public string? ObjectKind { get; }

    public PositionSnapshot? Destination { get; }

    public string SummaryText { get; }
}
