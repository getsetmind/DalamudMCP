namespace DalamudMCP.Infrastructure.Audit;

internal sealed record AuditLogEntry(DateTimeOffset Timestamp, string EventType, string Summary);
