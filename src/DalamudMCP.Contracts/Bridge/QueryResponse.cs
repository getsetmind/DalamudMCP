namespace DalamudMCP.Contracts.Bridge;

public sealed record QueryResponse<T>(
    bool Available,
    string? Reason,
    string ContractVersion,
    DateTimeOffset? CapturedAt,
    int? SnapshotAgeMs,
    T? Data);
