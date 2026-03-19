using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class DisableResourceUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;

    public DisableResourceUseCase(ISettingsRepository settingsRepository, IAuditLogWriter auditLogWriter)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
    }

    public async Task ExecuteAsync(string uriTemplate, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = policy.DisableResource(uriTemplate);
        await settingsRepository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(new AuditEvent(DateTimeOffset.UtcNow, "resource.disabled", uriTemplate.Trim()), cancellationToken).ConfigureAwait(false);
    }
}
