using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Services;
using DalamudMCP.Domain.Audit;
using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class UpdateExposurePolicyUseCase
{
    private readonly ISettingsRepository settingsRepository;
    private readonly IAuditLogWriter auditLogWriter;
    private readonly SettingsMutationGuard mutationGuard;

    public UpdateExposurePolicyUseCase(
        ISettingsRepository settingsRepository,
        IAuditLogWriter auditLogWriter,
        SettingsMutationGuard mutationGuard)
    {
        this.settingsRepository = settingsRepository;
        this.auditLogWriter = auditLogWriter;
        this.mutationGuard = mutationGuard;
    }

    public async Task ExecuteAsync(ExposurePolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        mutationGuard.EnsurePolicyAllowed(policy);

        await settingsRepository.SaveAsync(policy, cancellationToken).ConfigureAwait(false);
        await auditLogWriter.WriteAsync(
            new AuditEvent(
                DateTimeOffset.UtcNow,
                "settings.updated",
                $"tools={policy.EnabledTools.Count};resources={policy.EnabledResources.Count};addons={policy.EnabledAddons.Count}"),
            cancellationToken).ConfigureAwait(false);
    }
}
