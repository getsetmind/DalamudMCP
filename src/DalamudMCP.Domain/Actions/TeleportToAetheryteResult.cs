namespace DalamudMCP.Domain.Actions;

public sealed record TeleportToAetheryteResult
{
    public TeleportToAetheryteResult(
        string RequestedQuery,
        bool Succeeded,
        string? Reason,
        uint? AetheryteId,
        string? AetheryteName,
        string? TerritoryName,
        string SummaryText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RequestedQuery);
        this.RequestedQuery = RequestedQuery.Trim();
        this.Succeeded = Succeeded;
        this.Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();
        this.AetheryteId = AetheryteId;
        this.AetheryteName = string.IsNullOrWhiteSpace(AetheryteName) ? null : AetheryteName.Trim();
        this.TerritoryName = string.IsNullOrWhiteSpace(TerritoryName) ? null : TerritoryName.Trim();
        this.SummaryText = Snapshots.SnapshotGuard.RequiredText(SummaryText, nameof(SummaryText));
    }

    public string RequestedQuery { get; }

    public bool Succeeded { get; }

    public string? Reason { get; }

    public uint? AetheryteId { get; }

    public string? AetheryteName { get; }

    public string? TerritoryName { get; }

    public string SummaryText { get; }
}
