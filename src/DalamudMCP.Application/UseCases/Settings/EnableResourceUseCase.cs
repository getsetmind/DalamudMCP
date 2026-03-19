using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class EnableResourceUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;
    private readonly SettingsMutationGuard mutationGuard;

    public EnableResourceUseCase(
        ISettingsRepository settingsRepository,
        IAuditLogWriter auditLogWriter,
        SettingsMutationGuard mutationGuard)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
        this.mutationGuard = mutationGuard;
    }

    public async Task ExecuteAsync(string uriTemplate, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        mutationGuard.EnsureCanEnableResource(uriTemplate);
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = policy.EnableResource(uriTemplate);
        await settingsRepository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(new AuditEvent(DateTimeOffset.UtcNow, "resource.enabled", uriTemplate.Trim()), cancellationToken).ConfigureAwait(false);
    }
}
