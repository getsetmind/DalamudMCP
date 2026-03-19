namespace DalamudMCP.Domain.Audit;

public sealed record AuditEvent(DateTimeOffset Timestamp, string EventType, string Summary);
