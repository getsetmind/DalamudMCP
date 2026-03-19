using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class DisableToolUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;

    public DisableToolUseCase(ISettingsRepository settingsRepository, IAuditLogWriter auditLogWriter)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
    }

    public async Task ExecuteAsync(string toolName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = policy.DisableTool(toolName);
        await settingsRepository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(new AuditEvent(DateTimeOffset.UtcNow, "tool.disabled", toolName.Trim()), cancellationToken).ConfigureAwait(false);
    }
}
