using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class EnableAddonUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;
    private readonly SettingsMutationGuard mutationGuard;

    public EnableAddonUseCase(
        ISettingsRepository settingsRepository,
        IAuditLogWriter auditLogWriter,
        SettingsMutationGuard mutationGuard)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
        this.mutationGuard = mutationGuard;
    }

    public async Task ExecuteAsync(string addonName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        mutationGuard.EnsureCanEnableAddon(addonName);
        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = policy.EnableAddon(addonName);
        await settingsRepository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(new AuditEvent(DateTimeOffset.UtcNow, "addon.enabled", addonName.Trim()), cancellationToken).ConfigureAwait(false);
    }
}
