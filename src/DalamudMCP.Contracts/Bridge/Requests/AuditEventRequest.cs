namespace DalamudMCP.Contracts.Bridge.Requests;

public sealed record AuditEventRequest(string EventType, string Summary);
