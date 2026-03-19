using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class EnableToolUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;
    private readonly SettingsMutationGuard mutationGuard;

    public EnableToolUseCase(
        ISettingsRepository settingsRepository,
        IAuditLogWriter auditLogWriter,
        SettingsMutationGuard mutationGuard)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
        this.mutationGuard = mutationGuard;
    }

    public async Task ExecuteAsync(string toolName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        mutationGuard.EnsureCanEnableTool(toolName);
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = policy.EnableTool(toolName);
        await settingsRepository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(new AuditEvent(DateTimeOffset.UtcNow, "tool.enabled", toolName.Trim()), cancellationToken).ConfigureAwait(false);
    }
}
