using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class RecordAuditEventUseCase
{
    private readonly IAuditLogWriter auditLogWriter;

    public RecordAuditEventUseCase(IAuditLogWriter auditLogWriter)
    {
        this.auditLogWriter = auditLogWriter;
    }

    public Task ExecuteAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
        auditLogWriter.WriteAsync(auditEvent, cancellationToken);
}
