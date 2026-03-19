namespace DalamudMCP.Application.Common;

public static class QueryResults
{
    public static QueryResult<T> Success<T>(T value, DateTimeOffset capturedAt, int snapshotAgeMs) =>
        new(QueryStatus.Success, value, null, capturedAt, snapshotAgeMs);

    public static QueryResult<T> Disabled<T>(string reason) =>
        new(QueryStatus.Disabled, default, reason, null, null);

    public static QueryResult<T> Denied<T>(string reason) =>
        new(QueryStatus.Denied, default, reason, null, null);

    public static QueryResult<T> NotReady<T>(string reason) =>
        new(QueryStatus.NotReady, default, reason, null, null);
}
