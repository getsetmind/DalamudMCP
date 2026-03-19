using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Application.UseCases.Settings;

public sealed class GetCurrentSettingsUseCase
{
    private readonly ISettingsRepository settingsRepository;

    public GetCurrentSettingsUseCase(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;
    }

    public Task<ExposurePolicy> ExecuteAsync(CancellationToken cancellationToken) =>
        settingsRepository.LoadAsync(cancellationToken);
}
