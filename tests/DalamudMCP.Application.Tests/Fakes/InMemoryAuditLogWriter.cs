using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class InMemoryAuditLogWriter : IAuditLogWriter
{
    public List<AuditEvent> Events { get; } = [];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}
