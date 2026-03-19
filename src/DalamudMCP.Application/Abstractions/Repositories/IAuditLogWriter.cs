using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.Abstractions.Repositories;

public interface IAuditLogWriter
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
