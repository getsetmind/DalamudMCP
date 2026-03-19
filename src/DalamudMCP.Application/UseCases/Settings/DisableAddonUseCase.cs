using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class DisableAddonUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;

    public DisableAddonUseCase(ISettingsRepository settingsRepository, IAuditLogWriter auditLogWriter)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
    }

    public async Task ExecuteAsync(string addonName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = policy.DisableAddon(addonName);
        await settingsRepository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(new AuditEvent(DateTimeOffset.UtcNow, "addon.disabled", addonName.Trim()), cancellationToken).ConfigureAwait(false);
    }
}
