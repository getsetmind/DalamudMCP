namespace DalamudMCP.Application.Common;

public sealed record QueryResult<T>(
    QueryStatus Status,
    T? Value,
    string? Reason,
    DateTimeOffset? CapturedAt,
    int? SnapshotAgeMs)
{
    public bool IsSuccess => Status == QueryStatus.Success;
}
